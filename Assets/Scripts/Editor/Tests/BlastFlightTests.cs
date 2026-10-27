using NUnit.Framework;
using UnityEngine;

// Checks how the other machine plays back a blast flight, with no scene, network or ragdoll.
public class BlastFlightTests
{
    private static readonly Vector3 Gravity = new Vector3(0f, -10f, 0f);

    private static BlastFlight TwoSamples()
    {
        var flight = new BlastFlight();
        flight.Add(0f, Vector3.zero, Quaternion.identity, Vector3.up);
        flight.Add(1f, new Vector3(0f, 2f, 4f), Quaternion.Euler(0f, 90f, 0f), Vector3.up);
        return flight;
    }

    [Test]
    public void Evaluate_WithNoSamples_IsFalse()
    {
        Assert.IsFalse(new BlastFlight().Evaluate(0.5f, Gravity, out _, out _));
    }

    [Test]
    public void Evaluate_BetweenTwoSamples_Blends()
    {
        Assert.IsTrue(TwoSamples().Evaluate(0.5f, Gravity, out Vector3 position, out Quaternion rotation));
        Assert.AreEqual(0f, Vector3.Distance(new Vector3(0f, 1f, 2f), position), 0.0001f);
        Assert.AreEqual(45f, Quaternion.Angle(Quaternion.identity, rotation), 0.01f);
    }

    [Test]
    public void Evaluate_BeforeTheFirstSample_HoldsIt()
    {
        var flight = new BlastFlight();
        flight.Add(0.2f, Vector3.one, Quaternion.identity, Vector3.up);
        flight.Evaluate(0.1f, Gravity, out Vector3 position, out _);
        Assert.AreEqual(Vector3.one, position);
    }

    [Test]
    public void Evaluate_PastTheNewestSample_FollowsAThrowArc()
    {
        var flight = new BlastFlight();
        flight.Add(0f, Vector3.zero, Quaternion.identity, new Vector3(2f, 10f, 0f));
        flight.Evaluate(0.2f, Gravity, out Vector3 position, out _);

        // x = 2 * 0.2; y = 10 * 0.2 - 0.5 * 10 * 0.04
        Assert.AreEqual(0f, Vector3.Distance(new Vector3(0.4f, 1.8f, 0f), position), 0.0001f);
    }

    [Test]
    public void Evaluate_FarPastTheNewestSample_StopsGuessing()
    {
        var flight = new BlastFlight();
        flight.Add(0f, Vector3.zero, Quaternion.identity, Vector3.forward);
        flight.Evaluate(5f, Vector3.zero, out Vector3 position, out _);
        Assert.AreEqual(BlastFlight.MaxExtrapolation, position.z, 0.0001f);
    }

    [Test]
    public void Add_OutOfOrder_IsSortedByTime()
    {
        var flight = new BlastFlight();
        flight.Add(0f, Vector3.zero, Quaternion.identity, Vector3.zero);
        flight.Add(1f, new Vector3(0f, 0f, 10f), Quaternion.identity, Vector3.zero);
        flight.Add(0.5f, new Vector3(0f, 0f, 2f), Quaternion.identity, Vector3.zero);

        flight.Evaluate(0.25f, Gravity, out Vector3 position, out _);
        Assert.AreEqual(1f, position.z, 0.0001f);
    }

    [Test]
    public void Add_SameTimeTwice_KeepsOne()
    {
        var flight = TwoSamples();
        flight.Add(1f, Vector3.one * 100f, Quaternion.identity, Vector3.zero);
        Assert.AreEqual(2, flight.Count);
    }

    [Test]
    public void DropBefore_KeepsTheSampleJustBefore()
    {
        var flight = TwoSamples();
        flight.Add(2f, Vector3.zero, Quaternion.identity, Vector3.zero);
        flight.DropBefore(1.5f);
        Assert.AreEqual(2, flight.Count);

        // Still blends from the 1 s sample.
        flight.Evaluate(1.5f, Gravity, out Vector3 position, out _);
        Assert.AreEqual(0f, Vector3.Distance(new Vector3(0f, 1f, 2f), position), 0.0001f);
    }

    [Test]
    public void Land_HoldsTheRestingPlace()
    {
        var flight = TwoSamples();
        Quaternion[] pose = { Quaternion.identity };
        flight.Land(2f, new Vector3(5f, 0f, 5f), Quaternion.identity, pose);

        Assert.IsFalse(flight.IsLandedAt(1.9f));
        Assert.IsTrue(flight.IsLandedAt(2f));
        Assert.AreSame(pose, flight.LandPose);

        flight.Evaluate(3f, Gravity, out Vector3 position, out _);
        Assert.AreEqual(new Vector3(5f, 0f, 5f), position);
    }

    [Test]
    public void Clear_ForgetsTheLanding()
    {
        var flight = TwoSamples();
        flight.Land(2f, Vector3.zero, Quaternion.identity, null);
        flight.Clear();

        Assert.AreEqual(0, flight.Count);
        Assert.IsFalse(flight.HasLanding);
    }
}
