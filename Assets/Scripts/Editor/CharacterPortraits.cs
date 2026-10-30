using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Renders a head-and-shoulders picture of every character in the catalog for the lobby,
// so the menu needs no 3D preview at runtime. Run it again after a character is added or
// changed. The models are copied into a temporary empty scene that is never saved; the
// prefabs never change, and the scene that was open comes back afterwards.
// PreviewRenderUtility was tried first and crashed the Editor on the texture import that
// followed (RenderSettings manager missing), so a plain scene and camera are used instead.
public static class CharacterPortraits
{
    private const string CatalogPath = "Assets/Settings/CharacterCatalog.asset";
    private const string OutputFolder = "Assets/HUD/UI/Portraits";
    private const string PoseClipPath = "Assets/THRAX/Hodaart/HodaartLowPolyCharacterCollection3/Animations/Idle.anim";
    private const int Size = 256;
    private const float FieldOfView = 24f;

    // Height of the framed area, in metres. The Hodaart heads are big, so the whole head
    // and the shoulders need about a metre.
    private const float FrameHeight = 1.05f;

    // The humanoid head bone sits at the neck; the middle of the face is this far above it.
    private const float FaceAboveHeadBone = 0.25f;

    // Models stand this far apart, so each portrait shows one character.
    private const float Spacing = 10f;

    // Some renderers pick up new objects only on a later frame, so rendering waits this many Editor updates.
    private const int FramesToWait = 5;

    private static CharacterCatalog _catalog;
    private static GameObject[] _models;
    private static string _returnScene;
    private static int _framesLeft;

    [MenuItem("TagTheGame/Render Character Portraits")]
    public static void RenderAll()
    {
        if (_models != null)
        {
            Debug.LogWarning("Character portraits are already being rendered.");
            return;
        }

        _catalog = AssetDatabase.LoadAssetAtPath<CharacterCatalog>(CatalogPath);
        if (_catalog == null)
        {
            Debug.LogError("No character catalog at " + CatalogPath + ".");
            return;
        }

        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            if (EditorSceneManager.GetSceneAt(i).isDirty)
            {
                Debug.LogError("Save the open scenes first: rendering portraits opens a temporary scene.");
                return;
            }
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("Stop Play Mode before rendering portraits.");
            return;
        }

        _returnScene = EditorSceneManager.GetActiveScene().path;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        AnimationClip pose = AssetDatabase.LoadAssetAtPath<AnimationClip>(PoseClipPath);
        _models = new GameObject[_catalog.Count];
        for (int i = 0; i < _catalog.Count; i++)
        {
            GameObject prefab = _catalog.characters[i].model;
            if (prefab == null)
            {
                continue;
            }

            _models[i] = Object.Instantiate(prefab);

            // The models are stored in a T-pose; a frame of the idle clip lowers the arms.
            if (pose != null)
            {
                pose.SampleAnimation(_models[i], pose.length * 0.25f);
            }

            // Placed after the pose: a humanoid clip also sets the root position, which
            // would otherwise stack every model at the origin.
            _models[i].transform.SetPositionAndRotation(new Vector3(i * Spacing, 0f, 0f), Quaternion.identity);
        }

        AddLight(new Vector3(35f, 160f, 0f), 1.3f);
        AddLight(new Vector3(20f, -60f, 0f), 0.6f);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.5f, 0.5f, 0.55f);

        _framesLeft = FramesToWait;
        EditorApplication.update += WaitThenRender;
    }

    private static void AddLight(Vector3 euler, float intensity)
    {
        Light light = new GameObject("Portrait Light").AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = intensity;
        light.shadows = LightShadows.None;
        light.transform.rotation = Quaternion.Euler(euler);
    }

    private static void WaitThenRender()
    {
        if (_framesLeft-- > 0)
        {
            EditorApplication.QueuePlayerLoopUpdate();
            return;
        }

        EditorApplication.update -= WaitThenRender;
        string[] paths = new string[_models.Length];
        try
        {
            RenderModels(paths);
        }
        finally
        {
            _models = null;

            // Back to the scene the user had open; the temporary one is thrown away unsaved.
            if (!string.IsNullOrEmpty(_returnScene))
            {
                EditorSceneManager.OpenScene(_returnScene, OpenSceneMode.Single);
            }
            else
            {
                EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            }
        }

        ImportAndAssign(paths);
    }

    private static void RenderModels(string[] paths)
    {
        Directory.CreateDirectory(OutputFolder);
        Camera camera = new GameObject("Portrait Camera").AddComponent<Camera>();
        camera.fieldOfView = FieldOfView;
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 5f;
        camera.clearFlags = CameraClearFlags.SolidColor;

        // Transparent, so the card and roster colours show behind the character.
        camera.backgroundColor = Color.clear;
        RenderTexture target = RenderTexture.GetTemporary(Size, Size, 24, RenderTextureFormat.ARGB32);
        camera.targetTexture = target;
        try
        {
            for (int i = 0; i < _models.Length; i++)
            {
                if (_models[i] == null)
                {
                    continue;
                }

                AimAt(camera, _models[i]);
                camera.Render();

                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = target;
                Texture2D picture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
                picture.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                picture.Apply();
                RenderTexture.active = previous;

                paths[i] = OutputFolder + "/" + _catalog.characters[i].model.name + ".png";
                File.WriteAllBytes(paths[i], picture.EncodeToPNG());
                Object.DestroyImmediate(picture);
            }
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.ReleaseTemporary(target);
        }
    }

    // A little from the side, as in a character select screen, and level with the face.
    private static void AimAt(Camera camera, GameObject model)
    {
        Animator animator = model.GetComponent<Animator>();
        Transform head = animator != null ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
        Vector3 focus = head != null ? head.position + Vector3.up * FaceAboveHeadBone : model.transform.position + Vector3.up * 1.5f;
        Vector3 view = Quaternion.Euler(0f, 20f, 0f) * model.transform.forward;
        float distance = FrameHeight * 0.5f / Mathf.Tan(FieldOfView * 0.5f * Mathf.Deg2Rad);
        camera.transform.position = focus + view * distance;
        camera.transform.LookAt(focus);
    }

    private static void ImportAndAssign(string[] paths)
    {
        AssetDatabase.Refresh();
        int rendered = 0;
        for (int i = 0; i < paths.Length; i++)
        {
            if (paths[i] == null)
            {
                continue;
            }

            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(paths[i]);
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
            _catalog.characters[i].portrait = AssetDatabase.LoadAssetAtPath<Texture2D>(paths[i]);
            rendered++;
        }

        EditorUtility.SetDirty(_catalog);
        AssetDatabase.SaveAssets();
        Debug.Log("Rendered " + rendered + " character portraits into " + OutputFolder + ".");
    }
}
