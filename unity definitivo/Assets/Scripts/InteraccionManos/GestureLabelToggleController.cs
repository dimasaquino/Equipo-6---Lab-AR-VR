using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.UI;

[DisallowMultipleComponent]
public sealed class GestureLabelToggleController : MonoBehaviour
{
    // Deliberately not configurable: this detector never reads the right hand.
    public Handedness Hand => Handedness.Left;
    [SerializeField] private AnatomyLabelManager labelManager;
    [SerializeField] private Camera referenceCamera;
    [SerializeField] private Transform trackingSpace;
    [SerializeField] private XRDirectInteractor leftDirectInteractor;
    [SerializeField] private XRPokeInteractor leftPokeInteractor;
    [SerializeField] private XRRayInteractor leftRayInteractor;
    [SerializeField] private XRBaseInteractor[] leftInteractors;
    [SerializeField, Min(0.05f)] private float thumbUpConfirmationTime = 0.40f;
    [SerializeField, Min(0f)] private float cooldown = 0.80f;
    [SerializeField, Min(0f)] private float trackingJitterTolerance = 0.12f;
    [SerializeField, Min(0.05f)] private float neutralReleaseTime = 0.18f;
    [SerializeField] private bool debugGesture = false;

    private enum Sample { Invalid, Neutral, ThumbUp }
    private readonly List<XRHandSubsystem> subsystems = new List<XRHandSubsystem>();
    private XRHandSubsystem subsystem;
    private float nextSearch, lastUpdateTime = -1f;
    private float confirmation, interruption, neutralDuration, cooldownUntil;
    private bool latched, candidateLogged, trackingLost, blocked;
    private readonly Vector3[] thumb = new Vector3[4];
    private readonly Vector3[] fingers = new Vector3[16];

    private static readonly XRHandJointID[] ThumbIds =
    {
        XRHandJointID.ThumbMetacarpal, XRHandJointID.ThumbProximal,
        XRHandJointID.ThumbDistal, XRHandJointID.ThumbTip
    };
    private static readonly XRHandJointID[] FingerIds =
    {
        XRHandJointID.IndexProximal, XRHandJointID.IndexIntermediate, XRHandJointID.IndexDistal, XRHandJointID.IndexTip,
        XRHandJointID.MiddleProximal, XRHandJointID.MiddleIntermediate, XRHandJointID.MiddleDistal, XRHandJointID.MiddleTip,
        XRHandJointID.RingProximal, XRHandJointID.RingIntermediate, XRHandJointID.RingDistal, XRHandJointID.RingTip,
        XRHandJointID.LittleProximal, XRHandJointID.LittleIntermediate, XRHandJointID.LittleDistal, XRHandJointID.LittleTip
    };
    private void OnEnable() { lastUpdateTime = -1f; Acquire(); }
    private void OnDisable() { Unsubscribe(); CancelPartial(); }
    private void Update()
    {
        if (subsystem != null && !subsystem.running)
        {
            LoseTracking();
            Unsubscribe();
        }
        if (subsystem == null && Time.unscaledTime >= nextSearch) Acquire();
        if (lastUpdateTime >= 0f && Time.unscaledTime - lastUpdateTime > trackingJitterTolerance)
            LoseTracking();
    }
    private void Acquire()
    {
        nextSearch = Time.unscaledTime + 1f;
        SubsystemManager.GetSubsystems(subsystems);
        foreach (var item in subsystems)
        {
            if (item == null || !item.running) continue;
            if (subsystem == item) return;
            Unsubscribe();
            subsystem = item;
            subsystem.updatedHands += OnUpdatedHands;
            lastUpdateTime = -1f;
            return;
        }
    }
    private void Unsubscribe()
    {
        if (subsystem != null) subsystem.updatedHands -= OnUpdatedHands;
        subsystem = null;
    }
    private void CancelPartial()
    {
        confirmation = interruption = neutralDuration = 0f;
        candidateLogged = false;
        // Only a tracked Neutral can release a latch; loss/blocking cannot.
    }
    private void LoseTracking()
    {
        if (!trackingLost) Log("tracking perdido");
        trackingLost = true;
        CancelPartial();
    }
    private void OnUpdatedHands(XRHandSubsystem source, XRHandSubsystem.UpdateSuccessFlags flags,
        XRHandSubsystem.UpdateType updateType)
    {
        if (updateType != XRHandSubsystem.UpdateType.Dynamic) return;
        float now = Time.unscaledTime;
        if (lastUpdateTime >= 0f && now - lastUpdateTime > trackingJitterTolerance) CancelPartial();
        float delta = lastUpdateTime < 0f ? 0f : Mathf.Clamp(now - lastUpdateTime, 0f, 0.05f);
        lastUpdateTime = now;
        XRHand hand = source.leftHand;
        bool tracked = hand.isTracked;
        Sample sample = Sample.Invalid;
        if (tracked && (flags & XRHandSubsystem.UpdateSuccessFlags.LeftHandJoints) != 0)
            sample = ReadSample(hand);
        ProcessSample(sample, tracked, HasInteractionConflict(), delta, now);
    }
    private void ProcessSample(Sample sample, bool tracked, bool interactionBlocked, float delta, float now)
    {
        if (!tracked) { LoseTracking(); return; }
        trackingLost = false;
        if (interactionBlocked)
        {
            if (!blocked) Log("bloqueo por interacción");
            blocked = true;
            CancelPartial();
            return;
        }
        blocked = false;
        if (labelManager == null) { CancelPartial(); return; }
        if (sample == Sample.Invalid)
        {
            neutralDuration = 0f;
            interruption += delta;
            if (interruption > trackingJitterTolerance)
            {
                confirmation = 0f;
                candidateLogged = false;
            }
            return;
        }
        if (sample == Sample.Neutral)
        {
            interruption += delta;
            neutralDuration += delta;
            if (interruption > trackingJitterTolerance)
            {
                confirmation = 0f;
                candidateLogged = false;
            }
            if (neutralDuration >= neutralReleaseTime) latched = false;
            return;
        }
        interruption = neutralDuration = 0f;
        if (latched || now < cooldownUntil)
        {
            confirmation = 0f;
            return;
        }
        if (!candidateLogged) { Log("ThumbUpCandidate"); candidateLogged = true; }
        confirmation += delta;
        if (confirmation < thumbUpConfirmationTime) return;
        labelManager.ToggleLabels();
        latched = true;
        cooldownUntil = now + cooldown;
        CancelPartial();
        Log("ThumbUpConfirmed");
        Log(labelManager.LabelsEnabled ? "Labels ON" : "Labels OFF");
    }
    private Sample ReadSample(XRHand hand)
    {
        if (referenceCamera == null || trackingSpace == null) return Sample.Invalid;
        Vector3 palm, wrist, middle;
        if (!TryPosition(hand, XRHandJointID.Palm, out palm) ||
            !TryPosition(hand, XRHandJointID.Wrist, out wrist) ||
            !TryPosition(hand, XRHandJointID.MiddleMetacarpal, out middle)) return Sample.Invalid;
        for (int i = 0; i < ThumbIds.Length; i++)
            if (!TryPosition(hand, ThumbIds[i], out thumb[i])) return Sample.Invalid;
        for (int i = 0; i < FingerIds.Length; i++)
            if (!TryPosition(hand, FingerIds[i], out fingers[i])) return Sample.Invalid;
        // Joint poses are tracking-space poses; transform camera up into that same frame.
        Vector3 up = trackingSpace.InverseTransformDirection(referenceCamera.transform.up).normalized;
        return ClassifyGeometry(palm, wrist, middle, thumb, fingers, up);
    }
    private static Sample ClassifyGeometry(Vector3 palm, Vector3 wrist, Vector3 middle,
        Vector3[] thumbPoints, Vector3[] fingerPoints, Vector3 up)
    {
        float scale = Vector3.Distance(wrist, middle);
        if (!Finite(palm) || !Finite(wrist) || !Finite(middle) || !Finite(up) ||
            scale < 0.015f || up.sqrMagnitude < 0.5f) return Sample.Invalid;
        for (int i = 0; i < thumbPoints.Length; i++) if (!Finite(thumbPoints[i])) return Sample.Invalid;
        for (int i = 0; i < fingerPoints.Length; i++) if (!Finite(fingerPoints[i])) return Sample.Invalid;

        Vector3 a = thumbPoints[2] - thumbPoints[1], b = thumbPoints[3] - thumbPoints[2];
        float path = Vector3.Distance(thumbPoints[0], thumbPoints[1]) + a.magnitude + b.magnitude;
        if (a.sqrMagnitude < 0.000001f || b.sqrMagnitude < 0.000001f || path < 0.005f)
            return Sample.Invalid;
        float straightness = Vector3.Distance(thumbPoints[0], thumbPoints[3]) / path;
        Vector3 direction = (thumbPoints[3] - thumbPoints[1]).normalized;
        // About 49 degrees around camera-up allows wrist tilt, but rejects a lateral thumb.
        if (straightness < 0.80f || Vector3.Dot(a.normalized, b.normalized) < 0.65f ||
            Vector3.Distance(thumbPoints[3], palm) / scale < 1.05f ||
            Vector3.Dot(direction, up.normalized) < 0.65f ||
            Vector3.Dot(thumbPoints[3] - palm, up.normalized) / scale < 0.55f)
            return Sample.Neutral;
        // Explicit pinch rejection, independent of the other fingers.
        if (Vector3.Distance(thumbPoints[3], fingerPoints[3]) / scale < 0.45f)
            return Sample.Neutral;

        float total = 0f;
        for (int i = 0; i < 4; i++)
        {
            int offset = i * 4;
            Vector3 first = fingerPoints[offset + 1] - fingerPoints[offset];
            Vector3 second = fingerPoints[offset + 2] - fingerPoints[offset + 1];
            Vector3 third = fingerPoints[offset + 3] - fingerPoints[offset + 2];
            if (first.sqrMagnitude < 0.000001f || second.sqrMagnitude < 0.000001f ||
                third.sqrMagnitude < 0.000001f) return Sample.Invalid;
            float bend = Mathf.Min(Vector3.Dot(first.normalized, second.normalized),
                Mathf.Min(Vector3.Dot(second.normalized, third.normalized),
                    Vector3.Dot(first.normalized, third.normalized)));
            float distance = Vector3.Distance(fingerPoints[offset + 3], palm) / scale;
            float score = (1f - Mathf.InverseLerp(0.25f, 0.88f, bend)) * 0.65f +
                (1f - Mathf.InverseLerp(0.85f, 1.85f, distance)) * 0.35f;
            // Every finger must bend, with modest tolerance; average prevents four marginal fingers.
            if (bend > 0.70f || distance > 1.65f || score < 0.55f) return Sample.Neutral;
            total += score;
        }
        return total / 4f >= 0.68f ? Sample.ThumbUp : Sample.Neutral;
    }
    private static bool TryPosition(XRHand hand, XRHandJointID id, out Vector3 position)
    {
        XRHandJoint joint = hand.GetJoint(id);
        Pose pose;
        if ((joint.trackingState & XRHandJointTrackingState.Pose) != 0 && joint.TryGetPose(out pose) &&
            Finite(pose.position)) { position = pose.position; return true; }
        position = default;
        return false;
    }
    private static bool Finite(Vector3 v)
    {
        return !float.IsNaN(v.x) && !float.IsInfinity(v.x) && !float.IsNaN(v.y) &&
            !float.IsInfinity(v.y) && !float.IsNaN(v.z) && !float.IsInfinity(v.z);
    }
    private bool HasInteractionConflict()
    {
        if (leftInteractors != null)
            foreach (var interactor in leftInteractors)
                if (interactor != null && interactor.isActiveAndEnabled && interactor.hasSelection) return true;
        if (leftDirectInteractor != null && leftDirectInteractor.isActiveAndEnabled &&
            leftDirectInteractor.hasSelection) return true;
        TrackedDeviceModel model;
        if (leftPokeInteractor != null && leftPokeInteractor.isActiveAndEnabled &&
            (leftPokeInteractor.hasSelection || (leftPokeInteractor.TryGetUIModel(out model) &&
            (model.select || model.currentRaycast.isValid)))) return true;
        if (leftRayInteractor != null && leftRayInteractor.isActiveAndEnabled &&
            (leftRayInteractor.hasSelection || (leftRayInteractor.TryGetUIModel(out model) &&
            (model.select || model.currentRaycast.isValid)))) return true;
        return false;
    }
    private void Log(string message) { if (debugGesture) Debug.Log("LABEL GESTURE: " + message, this); }
}
