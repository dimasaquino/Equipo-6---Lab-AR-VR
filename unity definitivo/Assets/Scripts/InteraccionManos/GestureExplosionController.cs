using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.UI;

public class GestureExplosionController : MonoBehaviour
{
    public enum HandSelection { Right, Left }
    private enum HandState { Neutral, Closed, Open }
    private enum ExplosionMode { None, Huesos, Musculos, Inconsistent }

    private struct GestureSample
    {
        public bool valid;
        public bool pinch;
        public HandState state;
        public float openScore;
        public float closedScore;
        public int extendedFingers;
        public int curledFingers;
    }

    [Header("Explosion")]
    [SerializeField] private ControladorVisualBrazo controladorVisualBrazo;

    [Header("Hand")]
    [SerializeField] private HandSelection hand = HandSelection.Right;

    [Header("Right Hand Interactors")]
    [SerializeField] private XRDirectInteractor rightDirectInteractor;
    [SerializeField] private XRPokeInteractor rightPokeInteractor;
    [SerializeField] private XRRayInteractor rightRayInteractor;

    [Header("Timing")]
    [SerializeField, Min(0f)] private float closedPreparationTime = 0.20f;
    [SerializeField, Min(0f)] private float openConfirmationTime = 0.30f;
    [SerializeField, Min(0f)] private float closedConfirmationTime = 0.35f;
    [SerializeField, Min(0f)] private float cooldown = 0.75f;
    [SerializeField, Range(0.10f, 0.15f)] private float trackingJitterTolerance = 0.12f;

    [Header("Recognition")]
    [SerializeField, Range(0f, 1f)] private float openEnterThreshold = 0.70f;
    [SerializeField, Range(0f, 1f)] private float openExitThreshold = 0.55f;
    [SerializeField, Range(0f, 1f)] private float closedEnterThreshold = 0.70f;
    [SerializeField, Range(0f, 1f)] private float closedExitThreshold = 0.55f;

    [Header("Debug")]
    [SerializeField] private bool debugGesture = false;

    private readonly List<XRHandSubsystem> handSubsystems = new List<XRHandSubsystem>();
    private XRHandSubsystem handSubsystem;
    private HandState currentState = HandState.Neutral;
    private HandState latchedState = HandState.Neutral;
    private float stateDuration;
    private float invalidSampleDuration;
    private float nextSubsystemSearchTime;
    private float cooldownUntil;
    private bool armedForOpen;
    private bool readyForClose;
    private bool triggerLatch;
    private ExplosionMode lastMode = ExplosionMode.None;
    private bool lastModeOpen;
    private bool inconsistentModeWarningLogged;
    private bool stateInitialized;
    private GestureSample lastSample;

    private static readonly XRHandJointID[][] LongFingerJoints =
    {
        new[] { XRHandJointID.IndexMetacarpal, XRHandJointID.IndexProximal, XRHandJointID.IndexIntermediate, XRHandJointID.IndexDistal, XRHandJointID.IndexTip },
        new[] { XRHandJointID.MiddleMetacarpal, XRHandJointID.MiddleProximal, XRHandJointID.MiddleIntermediate, XRHandJointID.MiddleDistal, XRHandJointID.MiddleTip },
        new[] { XRHandJointID.RingMetacarpal, XRHandJointID.RingProximal, XRHandJointID.RingIntermediate, XRHandJointID.RingDistal, XRHandJointID.RingTip },
        new[] { XRHandJointID.LittleMetacarpal, XRHandJointID.LittleProximal, XRHandJointID.LittleIntermediate, XRHandJointID.LittleDistal, XRHandJointID.LittleTip }
    };

    private static readonly XRHandJointID[] ThumbJoints =
    {
        XRHandJointID.ThumbMetacarpal,
        XRHandJointID.ThumbProximal,
        XRHandJointID.ThumbDistal,
        XRHandJointID.ThumbTip
    };

    private void OnEnable()
    {
        SynchronizeWithController(true);
        TryAcquireSubsystem();
    }

    private void OnDisable()
    {
        UnsubscribeSubsystem();
        SetHandState(HandState.Neutral, lastSample);
        ResetProgress();
    }

    private void Update()
    {
        SynchronizeWithController(false);
        if (handSubsystem == null && Time.unscaledTime >= nextSubsystemSearchTime)
            TryAcquireSubsystem();
    }

    private void TryAcquireSubsystem()
    {
        nextSubsystemSearchTime = Time.unscaledTime + 1f;
        SubsystemManager.GetSubsystems(handSubsystems);

        XRHandSubsystem candidate = null;
        for (int i = 0; i < handSubsystems.Count; i++)
        {
            if (handSubsystems[i] != null && handSubsystems[i].running)
            {
                candidate = handSubsystems[i];
                break;
            }
        }

        if (candidate == null || handSubsystem == candidate)
            return;

        UnsubscribeSubsystem();
        handSubsystem = candidate;
        handSubsystem.updatedHands += OnUpdatedHands;
    }

    private void UnsubscribeSubsystem()
    {
        if (handSubsystem != null)
            handSubsystem.updatedHands -= OnUpdatedHands;
        handSubsystem = null;
    }

private void OnUpdatedHands(
        XRHandSubsystem subsystem,
        XRHandSubsystem.UpdateSuccessFlags updateSuccessFlags,
        XRHandSubsystem.UpdateType updateType)
    {
        if (updateType != XRHandSubsystem.UpdateType.Dynamic)
            return;

        SynchronizeWithController(false);

        ExplosionMode mode = GetActiveMode();
        if (mode == ExplosionMode.Inconsistent)
        {
            WarnInconsistentMode();
            CancelGestureProgress();
            return;
        }

        inconsistentModeWarningLogged = false;

        if (HasInteractionConflict() ||
            controladorVisualBrazo == null ||
            mode == ExplosionMode.None ||
            IsModeInTransition(mode))
        {
            CancelGestureProgress();
            return;
        }

        XRHand xrHand = hand == HandSelection.Right ? subsystem.rightHand : subsystem.leftHand;
        if (!xrHand.isTracked)
        {
            HandleInvalidSample();
            return;
        }

        GestureSample sample = ClassifyHand(xrHand);
        lastSample = sample;

        if (!sample.valid || sample.state == HandState.Neutral)
        {
            HandleInvalidSample();
            return;
        }

        invalidSampleDuration = 0f;
        SetHandState(sample.state, sample);

        if (triggerLatch)
        {
            if (currentState == latchedState)
                return;
            triggerLatch = false;
        }

        if (Time.unscaledTime < cooldownUntil)
            return;

        stateDuration += Time.unscaledDeltaTime;

        if (!IsModeOpen(mode))
            ProcessNormalState();
        else
            ProcessExplodedState();
    }

    private void HandleInvalidSample()
    {
        invalidSampleDuration += Time.unscaledDeltaTime;
        if (invalidSampleDuration <= trackingJitterTolerance)
            return;

        SetHandState(HandState.Neutral, lastSample);
        ResetProgress();
    }

    private void ProcessNormalState()
    {
        readyForClose = false;

        if (currentState == HandState.Closed)
        {
            if (!armedForOpen && stateDuration >= closedPreparationTime)
            {
                armedForOpen = true;
                LogEventWithScore("GESTURE: Armed");
            }
            return;
        }

        if (currentState == HandState.Open &&
            armedForOpen &&
            stateDuration >= openConfirmationTime)
        {
            RequestExplosion(false);
        }
    }

    private void ProcessExplodedState()
    {
        armedForOpen = false;

        if (currentState == HandState.Open)
        {
            readyForClose = true;
            return;
        }

        if (currentState == HandState.Closed &&
            readyForClose &&
            stateDuration >= closedConfirmationTime)
        {
            RequestExplosion(true);
        }
    }

private void RequestExplosion(bool currentlyOpen)
    {
        ExplosionMode mode = GetActiveMode();
        if (mode == ExplosionMode.Inconsistent)
        {
            WarnInconsistentMode();
            return;
        }

        if (controladorVisualBrazo == null ||
            mode == ExplosionMode.None ||
            IsModeOpen(mode) != currentlyOpen ||
            IsModeInTransition(mode))
            return;

        LogEventWithScore(currentlyOpen
            ? "GESTURE: EXPLOSION CLOSE REQUEST"
            : "GESTURE: EXPLOSION OPEN REQUEST");

        latchedState = currentState;
        triggerLatch = true;
        ToggleMode(mode);
        lastMode = mode;
        lastModeOpen = IsModeOpen(mode);
        cooldownUntil = Time.unscaledTime + cooldown;
        ResetProgress();
    }

private void SynchronizeWithController(bool force)
    {
        if (controladorVisualBrazo == null)
            return;

        ExplosionMode actualMode = GetActiveMode();
        bool actualOpen =
            actualMode != ExplosionMode.None &&
            actualMode != ExplosionMode.Inconsistent &&
            IsModeOpen(actualMode);

        if (!stateInitialized)
        {
            lastMode = actualMode;
            lastModeOpen = actualOpen;
            stateInitialized = true;
            return;
        }

        if (!force &&
            actualMode == lastMode &&
            actualOpen == lastModeOpen)
            return;

        lastMode = actualMode;
        lastModeOpen = actualOpen;
        latchedState = currentState;
        triggerLatch = true;
        cooldownUntil = Time.unscaledTime + cooldown;
        CancelGestureProgress();
    }

private ExplosionMode GetActiveMode()
    {
        if (controladorVisualBrazo == null)
            return ExplosionMode.None;

        bool huesos = controladorVisualBrazo.HuesosVisibles;
        bool musculos = controladorVisualBrazo.MusculosVisibles;

        if (huesos && musculos)
            return ExplosionMode.Inconsistent;
        if (huesos)
            return ExplosionMode.Huesos;
        if (musculos)
            return ExplosionMode.Musculos;

        return ExplosionMode.None;
    }

    private bool IsModeOpen(ExplosionMode mode)
    {
        return mode == ExplosionMode.Huesos
            ? controladorVisualBrazo.HuesosAbiertos
            : mode == ExplosionMode.Musculos &&
              controladorVisualBrazo.MusculosAbiertos;
    }

    private bool IsModeInTransition(ExplosionMode mode)
    {
        return mode == ExplosionMode.Huesos
            ? controladorVisualBrazo.HuesosEnTransicion
            : mode == ExplosionMode.Musculos &&
              controladorVisualBrazo.MusculosEnTransicion;
    }

    private void ToggleMode(ExplosionMode mode)
    {
        if (mode == ExplosionMode.Huesos)
            controladorVisualBrazo.AlternarExplosionHuesos();
        else if (mode == ExplosionMode.Musculos)
            controladorVisualBrazo.AlternarExplosionMusculos();
    }

    private void WarnInconsistentMode()
    {
        if (!debugGesture || inconsistentModeWarningLogged)
            return;

        inconsistentModeWarningLogged = true;
        Debug.LogWarning(
            "GESTURE: Huesos y músculos están visibles simultáneamente. No se ejecutará ninguna explosión.",
            this);
    }


    private void CancelGestureProgress()
    {
        invalidSampleDuration = 0f;
        SetHandState(HandState.Neutral, lastSample);
        ResetProgress();
    }

    private void ResetProgress()
    {
        stateDuration = 0f;
        invalidSampleDuration = 0f;
        armedForOpen = false;
        readyForClose = false;
    }

    private void SetHandState(HandState newState, GestureSample sample)
    {
        if (currentState == newState)
            return;

        LogFailedConfirmationIfNeeded();
        currentState = newState;
        stateDuration = 0f;

        if (!debugGesture)
            return;

        switch (newState)
        {
            case HandState.Closed:
                Debug.Log("GESTURE: Closed", this);
                break;
            case HandState.Open:
                Debug.Log("GESTURE: Open", this);
                break;
            default:
                Debug.Log("GESTURE: Neutral", this);
                break;
        }

        LogScore(sample);
    }

private void LogFailedConfirmationIfNeeded()
    {
        if (!debugGesture || controladorVisualBrazo == null)
            return;

        ExplosionMode mode = GetActiveMode();
        if (mode == ExplosionMode.None ||
            mode == ExplosionMode.Inconsistent)
            return;

        float required = 0f;
        if (!IsModeOpen(mode))
        {
            if (currentState == HandState.Closed && !armedForOpen)
                required = closedPreparationTime;
            else if (currentState == HandState.Open && armedForOpen)
                required = openConfirmationTime;
        }
        else if (currentState == HandState.Closed && readyForClose)
        {
            required = closedConfirmationTime;
        }

        if (required > 0f && stateDuration >= required * 0.5f && stateDuration < required)
        {
            Debug.Log("GESTURE: Confirmation failed after 50% progress", this);
            LogScore(lastSample);
        }
    }

    private void LogEventWithScore(string message)
    {
        if (!debugGesture)
            return;

        Debug.Log(message, this);
        LogScore(lastSample);
    }

    private void LogScore(GestureSample sample)
    {
        if (!debugGesture)
            return;

        Debug.LogFormat(
            this,
            "GESTURE SCORE: OpenScore = {0:F2} ClosedScore = {1:F2} Extended fingers = {2} Curled fingers = {3}",
            sample.openScore,
            sample.closedScore,
            sample.extendedFingers,
            sample.curledFingers);
    }

    private bool HasInteractionConflict()
    {
        if (rightDirectInteractor != null && rightDirectInteractor.hasSelection)
            return true;

        if (rightPokeInteractor != null)
        {
            if (rightPokeInteractor.hasSelection)
                return true;

            TrackedDeviceModel pokeModel;
            if (rightPokeInteractor.TryGetUIModel(out pokeModel) &&
                (pokeModel.select || pokeModel.currentRaycast.isValid))
                return true;
        }

        if (rightRayInteractor != null)
        {
            if (rightRayInteractor.hasSelection)
                return true;

            TrackedDeviceModel rayModel;
            if (rightRayInteractor.TryGetUIModel(out rayModel) &&
                (rayModel.select || rayModel.currentRaycast.isValid))
                return true;
        }

        return false;
    }

    private GestureSample ClassifyHand(XRHand xrHand)
    {
        GestureSample sample = new GestureSample { state = HandState.Neutral };

        Vector3 palm;
        Vector3 wrist;
        Vector3 middleMetacarpal;
        if (!TryGetPosition(xrHand, XRHandJointID.Palm, out palm) ||
            !TryGetPosition(xrHand, XRHandJointID.Wrist, out wrist) ||
            !TryGetPosition(xrHand, XRHandJointID.MiddleMetacarpal, out middleMetacarpal))
            return sample;

        float handScale = Vector3.Distance(wrist, middleMetacarpal);
        if (handScale < 0.015f)
            return sample;

        float openTotal = 0f;
        float closedTotal = 0f;
        int partialExtended = 0;
        int partialCurled = 0;
        float[] fingerOpenScores = new float[4];
        Vector3[] tips = new Vector3[4];

        for (int i = 0; i < LongFingerJoints.Length; i++)
        {
            Vector3[] points;
            if (!TryGetChain(xrHand, LongFingerJoints[i], out points))
                return sample;

            tips[i] = points[4];
            fingerOpenScores[i] = GetOpenScore(points, palm, handScale);
            float curledScore = GetClosedScore(points, palm, wrist, handScale);
            openTotal += fingerOpenScores[i];
            closedTotal += curledScore;

            if (fingerOpenScores[i] >= 0.70f)
                sample.extendedFingers++;
            else if (fingerOpenScores[i] >= 0.42f)
                partialExtended++;

            if (curledScore >= 0.70f)
                sample.curledFingers++;
            else if (curledScore >= 0.42f)
                partialCurled++;
        }

        Vector3[] thumb;
        if (!TryGetChain(xrHand, ThumbJoints, out thumb))
            return sample;

        float thumbScore = GetThumbOpenScore(thumb, palm, handScale);
        sample.openScore = Mathf.Clamp01((openTotal / 4f) * 0.95f + thumbScore * 0.05f);
        sample.closedScore = Mathf.Clamp01(closedTotal / 4f);
        sample.valid = true;

        float pinchDistance = Vector3.Distance(thumb[3], tips[0]) / handScale;
        int protectedExtended =
            (fingerOpenScores[1] >= 0.62f ? 1 : 0) +
            (fingerOpenScores[2] >= 0.62f ? 1 : 0) +
            (fingerOpenScores[3] >= 0.62f ? 1 : 0);

        if (pinchDistance < 0.40f && protectedExtended >= 2)
        {
            sample.pinch = true;
            sample.state = HandState.Neutral;
            return sample;
        }

        float openThreshold = currentState == HandState.Open
            ? openExitThreshold
            : openEnterThreshold;
        float closedThreshold = currentState == HandState.Closed
            ? closedExitThreshold
            : closedEnterThreshold;

        bool open =
            sample.extendedFingers >= 3 &&
            sample.extendedFingers + partialExtended >= 4 &&
            sample.openScore >= openThreshold;

        bool closed =
            sample.curledFingers >= 3 &&
            sample.curledFingers + partialCurled >= 4 &&
            sample.closedScore >= closedThreshold;

        if (open && !closed)
            sample.state = HandState.Open;
        else if (closed && !open)
            sample.state = HandState.Closed;

        return sample;
    }

    private static float GetOpenScore(Vector3[] p, Vector3 palm, float scale)
    {
        float path = 0f;
        for (int i = 0; i < p.Length - 1; i++)
            path += Vector3.Distance(p[i], p[i + 1]);

        float straightness = Vector3.Distance(p[0], p[4]) / Mathf.Max(path, 0.0001f);
        float alignment =
            (Vector3.Dot((p[2] - p[1]).normalized, (p[3] - p[2]).normalized) +
             Vector3.Dot((p[3] - p[2]).normalized, (p[4] - p[3]).normalized)) * 0.5f;
        float tipDistance = Vector3.Distance(p[4], palm) / scale;

        float straightnessScore = Mathf.InverseLerp(0.60f, 0.92f, straightness);
        float alignmentScore = Mathf.InverseLerp(0.20f, 0.90f, alignment);
        float distanceScore = Mathf.InverseLerp(0.95f, 1.75f, tipDistance);

        return Mathf.Clamp01(
            straightnessScore * 0.30f +
            alignmentScore * 0.35f +
            distanceScore * 0.35f);
    }

    private static float GetClosedScore(
        Vector3[] p,
        Vector3 palm,
        Vector3 wrist,
        float scale)
    {
        Vector3 first = (p[2] - p[1]).normalized;
        Vector3 second = (p[3] - p[2]).normalized;
        Vector3 third = (p[4] - p[3]).normalized;

        float proximalBend = Vector3.Dot(first, second);
        float distalBend = Vector3.Dot(second, third);
        float foldedDirection = Vector3.Dot(first, third);
        float strongestBend = Mathf.Min(proximalBend, Mathf.Min(distalBend, foldedDirection));
        float palmDistance = Vector3.Distance(p[4], palm) / scale;
        float wristDistance = Vector3.Distance(p[4], wrist) / scale;

        float bendScore = 1f - Mathf.InverseLerp(0.25f, 0.88f, strongestBend);
        float palmScore = 1f - Mathf.InverseLerp(0.85f, 1.85f, palmDistance);
        float wristScore = 1f - Mathf.InverseLerp(1.45f, 2.65f, wristDistance);

        return Mathf.Clamp01(
            bendScore * 0.45f +
            palmScore * 0.40f +
            wristScore * 0.15f);
    }

    private static float GetThumbOpenScore(Vector3[] p, Vector3 palm, float scale)
    {
        float path =
            Vector3.Distance(p[0], p[1]) +
            Vector3.Distance(p[1], p[2]) +
            Vector3.Distance(p[2], p[3]);
        float straightness = Vector3.Distance(p[0], p[3]) / Mathf.Max(path, 0.0001f);
        float palmDistance = Vector3.Distance(p[3], palm) / scale;

        return Mathf.Clamp01(
            Mathf.InverseLerp(0.55f, 0.90f, straightness) * 0.5f +
            Mathf.InverseLerp(0.55f, 1.20f, palmDistance) * 0.5f);
    }

    private static bool TryGetChain(
        XRHand handData,
        XRHandJointID[] jointIds,
        out Vector3[] points)
    {
        points = new Vector3[jointIds.Length];
        for (int i = 0; i < jointIds.Length; i++)
        {
            if (!TryGetPosition(handData, jointIds[i], out points[i]))
                return false;
        }
        return true;
    }

    private static bool TryGetPosition(
        XRHand handData,
        XRHandJointID jointId,
        out Vector3 position)
    {
        Pose pose;
        if (handData.GetJoint(jointId).TryGetPose(out pose))
        {
            position = pose.position;
            return true;
        }

        position = default(Vector3);
        return false;
    }
}
