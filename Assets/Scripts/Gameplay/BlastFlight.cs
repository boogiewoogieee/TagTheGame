using System.Collections.Generic;
using UnityEngine;

// One body's flight as the other machine receives it: samples of the owner's hips, timed in
// game seconds since the launch, and the pose the body came to rest in. Plain C#, so the
// playback rules can be checked without a network or a ragdoll.
public class BlastFlight
{
    public struct Sample
    {
        public float Time;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Velocity;
    }

    // Longest guess past the newest sample before the hips just wait for the next one.
    public const float MaxExtrapolation = 0.25f;

    public int Count => _samples.Count;

    public bool HasLanding { get; private set; }
    public float LandTime { get; private set; }
    public Vector3 LandPosition { get; private set; }
    public Quaternion LandRotation { get; private set; }

    // Local rotations of the parts below the hips, in the ragdoll's build order.
    public Quaternion[] LandPose { get; private set; }

    private readonly List<Sample> _samples = new List<Sample>();

    public void Clear()
    {
        _samples.Clear();
        HasLanding = false;
        LandPose = null;
    }

    public void Add(float time, Vector3 position, Quaternion rotation, Vector3 velocity)
    {
        // Unreliable messages may come late or out of order; keep the list sorted by time.
        int index = _samples.Count;
        while (index > 0 && _samples[index - 1].Time >= time)
        {
            if (Mathf.Approximately(_samples[index - 1].Time, time))
            {
                return;
            }

            index--;
        }

        _samples.Insert(index, new Sample { Time = time, Position = position, Rotation = rotation, Velocity = velocity });
    }

    public void Land(float time, Vector3 position, Quaternion rotation, Quaternion[] pose)
    {
        Add(time, position, rotation, Vector3.zero);
        HasLanding = true;
        LandTime = time;
        LandPosition = position;
        LandRotation = rotation;
        LandPose = pose;
    }

    public bool IsLandedAt(float time)
    {
        return HasLanding && time >= LandTime;
    }

    // Forgets the samples that playback has passed, keeping the one just before the time.
    public void DropBefore(float time)
    {
        while (_samples.Count > 1 && _samples[1].Time <= time)
        {
            _samples.RemoveAt(0);
        }
    }

    // Where the owner's hips were at this time: blended between the two samples around it,
    // or carried along a throw arc past the newest one. False while there is no sample yet.
    public bool Evaluate(float time, Vector3 gravity, out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;
        if (_samples.Count == 0)
        {
            return false;
        }

        int next = 0;
        while (next < _samples.Count && _samples[next].Time <= time)
        {
            next++;
        }

        if (next == 0)
        {
            position = _samples[0].Position;
            rotation = _samples[0].Rotation;
            return true;
        }

        Sample from = _samples[next - 1];
        if (next < _samples.Count)
        {
            Sample to = _samples[next];
            float t = (time - from.Time) / Mathf.Max(to.Time - from.Time, 0.0001f);
            position = Vector3.Lerp(from.Position, to.Position, t);
            rotation = Quaternion.Slerp(from.Rotation, to.Rotation, t);
            return true;
        }

        if (IsLandedAt(time))
        {
            position = LandPosition;
            rotation = LandRotation;
            return true;
        }

        float ahead = Mathf.Clamp(time - from.Time, 0f, MaxExtrapolation);
        position = from.Position + from.Velocity * ahead + 0.5f * ahead * ahead * gravity;
        rotation = from.Rotation;
        return true;
    }
}
