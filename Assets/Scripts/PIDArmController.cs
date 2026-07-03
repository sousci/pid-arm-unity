using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class PIDArmController : MonoBehaviour
{
    public const float DefaultPGain = 20f;
    public const float DefaultIGain = 0f;
    public const float DefaultDGain = 5f;
    public const float DefaultTargetAngle = 30f;
    public const float DefaultArmLength = 2f;
    public const float DefaultArmMass = 1f;
    public const float MaxTorque = 100f;

    const float ArmWidth = 0.18f;
    const float IntegralLimit = 25f;
    const float ConvergedErrorDegrees = 1f;
    const float ConvergedAngularVelocity = 2f;
    const float ConvergedHoldSeconds = 0.5f;
    const int MaxTrailPoints = 420;
    const float GraphDurationSeconds = 10f;
    const float GraphAngleRange = 90f;
    const float GraphSampleInterval = 0.04f;

    Rigidbody2D armBody;
    BoxCollider2D armCollider;
    HingeJoint2D hinge;
    LineRenderer armLine;
    LineRenderer targetLine;
    LineRenderer currentLine;
    LineRenderer downReferenceLine;
    LineRenderer trailLine;
    LineRenderer angleGraphLine;
    LineRenderer targetGraphLine;
    Transform pivotTransform;
    Transform tipMarker;
    Vector3 graphOrigin;
    Vector2 graphSize;

    Text currentAngleText;
    Text targetAngleText;
    Text outputText;
    Text errorText;
    Text pTermText;
    Text iTermText;
    Text dTermText;
    Text pGainText;
    Text iGainText;
    Text dGainText;
    Text armLengthText;
    Text armMassText;
    Text convergenceText;
    Text overshootText;

    readonly List<Vector3> trailPoints = new List<Vector3>();
    readonly List<Vector2> angleSamples = new List<Vector2>();

    float pGain = DefaultPGain;
    float iGain = DefaultIGain;
    float dGain = DefaultDGain;
    float targetAngle = DefaultTargetAngle;
    float armLength = DefaultArmLength;
    float armMass = DefaultArmMass;

    bool controlEnabled;
    float integral;
    float previousError;
    float outputTorque;
    float pTerm;
    float iTerm;
    float dTerm;
    float stableTime;
    float overshoot;
    float graphStartTime;
    float lastGraphSampleTime;

    public void Configure(
        Rigidbody2D body,
        BoxCollider2D collider,
        HingeJoint2D joint,
        Transform pivot,
        Transform tip,
        LineRenderer arm,
        LineRenderer target,
        LineRenderer current,
        LineRenderer downReference,
        LineRenderer trail,
        LineRenderer angleGraph,
        LineRenderer targetGraph,
        Vector3 angleGraphOrigin,
        Vector2 angleGraphSize,
        Text currentAngle,
        Text targetAngleValue,
        Text output,
        Text error,
        Text pTermValue,
        Text iTermValue,
        Text dTermValue,
        Text pGainValue,
        Text iGainValue,
        Text dGainValue,
        Text armLengthValue,
        Text armMassValue,
        Text convergence,
        Text overshootValue)
    {
        armBody = body;
        armCollider = collider;
        hinge = joint;
        pivotTransform = pivot;
        tipMarker = tip;
        armLine = arm;
        targetLine = target;
        currentLine = current;
        downReferenceLine = downReference;
        trailLine = trail;
        angleGraphLine = angleGraph;
        targetGraphLine = targetGraph;
        graphOrigin = angleGraphOrigin;
        graphSize = angleGraphSize;

        currentAngleText = currentAngle;
        targetAngleText = targetAngleValue;
        outputText = output;
        errorText = error;
        pTermText = pTermValue;
        iTermText = iTermValue;
        dTermText = dTermValue;
        pGainText = pGainValue;
        iGainText = iGainValue;
        dGainText = dGainValue;
        armLengthText = armLengthValue;
        armMassText = armMassValue;
        convergenceText = convergence;
        overshootText = overshootValue;

        ApplyArmGeometry();
        ResetSimulation();
    }

    void FixedUpdate()
    {
        if (armBody == null)
        {
            return;
        }

        // Angle convention for this lesson: straight down is 0 deg, right rotation is positive.
        float currentAngle = GetCurrentAngle();
        float error = Mathf.DeltaAngle(currentAngle, targetAngle);

        if (controlEnabled)
        {
            ApplyPidTorque(error);
        }
        else
        {
            previousError = error;
            outputTorque = 0f;
            pTerm = pGain * error;
            iTerm = iGain * integral;
            dTerm = 0f;
        }

        UpdateConvergence(error);
        UpdateOvershoot(currentAngle);
        RecordAngleSample(currentAngle);
    }

    void Update()
    {
        if (armBody == null)
        {
            return;
        }

        UpdateVisuals();
        UpdateReadouts();
    }

    public void StartControl()
    {
        controlEnabled = true;
        integral = 0f;
        previousError = Mathf.DeltaAngle(GetCurrentAngle(), targetAngle);
        stableTime = 0f;
        overshoot = 0f;
        graphStartTime = Time.time;
        lastGraphSampleTime = -GraphSampleInterval;
        angleSamples.Clear();
    }

    public void ResetSimulation()
    {
        controlEnabled = false;
        integral = 0f;
        previousError = targetAngle;
        outputTorque = 0f;
        pTerm = 0f;
        iTerm = 0f;
        dTerm = 0f;
        stableTime = 0f;
        overshoot = 0f;
        graphStartTime = Time.time;
        lastGraphSampleTime = -GraphSampleInterval;
        trailPoints.Clear();
        angleSamples.Clear();

        if (armBody != null)
        {
            armBody.linearVelocity = Vector2.zero;
            armBody.angularVelocity = 0f;
            armBody.rotation = 0f;
            armBody.position = pivotTransform.position;
        }

        UpdateVisuals();
        UpdateReadouts();
    }

    public void SetPGain(float value)
    {
        pGain = value;
    }

    public void SetIGain(float value)
    {
        iGain = value;
    }

    public void SetDGain(float value)
    {
        dGain = value;
    }

    public void SetTargetAngle(float value)
    {
        targetAngle = value;
        stableTime = 0f;
        overshoot = 0f;
    }

    public void SetArmLength(float value)
    {
        armLength = value;
        ApplyArmGeometry();
        trailPoints.Clear();
    }

    public void SetArmMass(float value)
    {
        armMass = value;
        if (armBody != null)
        {
            armBody.mass = armMass;
        }
    }

    void ApplyPidTorque(float error)
    {
        float dt = Time.fixedDeltaTime;

        // Integral windup and excessive torque make the pendulum hard to read, so both are clamped.
        integral = Mathf.Clamp(integral + error * dt, -IntegralLimit, IntegralLimit);
        float derivative = (error - previousError) / dt;

        pTerm = pGain * error;
        iTerm = iGain * integral;
        dTerm = dGain * derivative;

        outputTorque = Mathf.Clamp(pTerm + iTerm + dTerm, -MaxTorque, MaxTorque);
        armBody.AddTorque(outputTorque, ForceMode2D.Force);

        previousError = error;
    }

    void ApplyArmGeometry()
    {
        if (armCollider != null)
        {
            // The Rigidbody transform sits at the pivot. Collider and center of mass extend downward.
            armCollider.size = new Vector2(ArmWidth, armLength);
            armCollider.offset = new Vector2(0f, -armLength * 0.5f);
        }

        if (armBody != null)
        {
            armBody.mass = armMass;
            armBody.centerOfMass = new Vector2(0f, -armLength * 0.5f);
            armBody.angularDamping = 0f;
            armBody.linearDamping = 0f;
            armBody.gravityScale = 1f;
        }

        if (hinge != null)
        {
            hinge.autoConfigureConnectedAnchor = false;
            hinge.anchor = Vector2.zero;
            hinge.connectedAnchor = Vector2.zero;
            hinge.useMotor = false;
            hinge.useLimits = false;
        }
    }

    void UpdateConvergence(float error)
    {
        // A response is treated as converged only after the error and speed stay small for a while.
        bool closeEnough = Mathf.Abs(error) <= ConvergedErrorDegrees &&
            Mathf.Abs(armBody.angularVelocity) <= ConvergedAngularVelocity;

        stableTime = closeEnough ? stableTime + Time.fixedDeltaTime : 0f;
    }

    void UpdateOvershoot(float currentAngle)
    {
        if (!controlEnabled || Mathf.Abs(targetAngle) < 0.001f)
        {
            return;
        }

        float targetDirection = Mathf.Sign(targetAngle);
        float beyondTarget = (currentAngle - targetAngle) * targetDirection;
        overshoot = Mathf.Max(overshoot, beyondTarget);
    }

    void UpdateVisuals()
    {
        Vector3 pivot = pivotTransform.position;
        Vector3 tip = GetTipPosition();

        SetLine(armLine, pivot, tip);
        SetLine(targetLine, pivot, GetDirectionPoint(targetAngle, armLength + 0.45f));
        SetLine(currentLine, pivot, GetDirectionPoint(GetCurrentAngle(), armLength + 0.25f));
        SetLine(downReferenceLine, pivot, pivot + Vector3.down * (armLength + 0.6f));
        UpdateAngleGraph();

        if (tipMarker != null)
        {
            tipMarker.position = tip;
        }

        AddTrailPoint(tip);
    }

    void UpdateReadouts()
    {
        float currentAngle = GetCurrentAngle();
        float error = Mathf.DeltaAngle(currentAngle, targetAngle);

        SetText(currentAngleText, $"Current angle: {currentAngle,6:0.0} deg");
        SetText(targetAngleText, $"Target angle : {targetAngle,6:0.0} deg");
        SetText(errorText, $"Error        : {error,6:0.0} deg");
        SetText(outputText, $"PID torque   : {outputTorque,6:0.0}");
        SetText(pTermText, $"P term       : {pTerm,6:0.0}");
        SetText(iTermText, $"I term       : {iTerm,6:0.0}");
        SetText(dTermText, $"D term       : {dTerm,6:0.0}");
        SetText(pGainText, $"P gain: {pGain:0.0}");
        SetText(iGainText, $"I gain: {iGain:0.0}");
        SetText(dGainText, $"D gain: {dGain:0.0}");
        SetText(armLengthText, $"Arm length: {armLength:0.00} m");
        SetText(armMassText, $"Arm mass: {armMass:0.00} kg");
        SetText(overshootText, $"Overshoot: {Mathf.Max(0f, overshoot):0.0} deg");
        SetText(convergenceText, stableTime >= ConvergedHoldSeconds ? "State: \u53ce\u675f" : "State: \u672a\u53ce\u675f");
    }

    float GetCurrentAngle()
    {
        return Mathf.DeltaAngle(0f, armBody.rotation);
    }

    Vector3 GetTipPosition()
    {
        return armBody.transform.TransformPoint(new Vector3(0f, -armLength, 0f));
    }

    Vector3 GetDirectionPoint(float angleDegrees, float length)
    {
        float radians = angleDegrees * Mathf.Deg2Rad;
        Vector3 direction = new Vector3(Mathf.Sin(radians), -Mathf.Cos(radians), 0f);
        return pivotTransform.position + direction * length;
    }

    void AddTrailPoint(Vector3 tip)
    {
        if (!controlEnabled)
        {
            return;
        }

        // Keep a bounded trail so long experiments do not allocate unbounded line points.
        if (trailPoints.Count == 0 || Vector3.Distance(trailPoints[trailPoints.Count - 1], tip) > 0.015f)
        {
            trailPoints.Add(tip);
            if (trailPoints.Count > MaxTrailPoints)
            {
                trailPoints.RemoveAt(0);
            }
        }

        if (trailLine != null)
        {
            trailLine.positionCount = trailPoints.Count;
            trailLine.SetPositions(trailPoints.ToArray());
        }
    }

    void RecordAngleSample(float currentAngle)
    {
        if (!controlEnabled)
        {
            return;
        }

        float elapsed = Time.time - graphStartTime;
        if (elapsed - lastGraphSampleTime < GraphSampleInterval)
        {
            return;
        }

        lastGraphSampleTime = elapsed;
        angleSamples.Add(new Vector2(elapsed, currentAngle));

        float cutoff = elapsed - GraphDurationSeconds;
        while (angleSamples.Count > 0 && angleSamples[0].x < cutoff)
        {
            angleSamples.RemoveAt(0);
        }
    }

    void UpdateAngleGraph()
    {
        UpdateTargetGraphLine();

        if (angleGraphLine == null)
        {
            return;
        }

        if (angleSamples.Count == 0)
        {
            angleGraphLine.positionCount = 0;
            return;
        }

        float currentTime = controlEnabled ? Time.time - graphStartTime : angleSamples[angleSamples.Count - 1].x;
        Vector3[] points = new Vector3[angleSamples.Count];
        for (int i = 0; i < angleSamples.Count; i++)
        {
            points[i] = MapGraphPoint(angleSamples[i].x, angleSamples[i].y, currentTime);
        }

        angleGraphLine.positionCount = points.Length;
        angleGraphLine.SetPositions(points);
    }

    void UpdateTargetGraphLine()
    {
        if (targetGraphLine == null)
        {
            return;
        }

        float y = MapGraphY(targetAngle);
        targetGraphLine.positionCount = 2;
        targetGraphLine.SetPosition(0, new Vector3(graphOrigin.x, y, graphOrigin.z));
        targetGraphLine.SetPosition(1, new Vector3(graphOrigin.x + graphSize.x, y, graphOrigin.z));
    }

    Vector3 MapGraphPoint(float sampleTime, float angle, float currentTime)
    {
        float normalizedTime = 1f - Mathf.Clamp01((currentTime - sampleTime) / GraphDurationSeconds);
        return new Vector3(
            graphOrigin.x + normalizedTime * graphSize.x,
            MapGraphY(angle),
            graphOrigin.z);
    }

    float MapGraphY(float angle)
    {
        float normalizedAngle = Mathf.InverseLerp(-GraphAngleRange, GraphAngleRange, Mathf.Clamp(angle, -GraphAngleRange, GraphAngleRange));
        return graphOrigin.y + normalizedAngle * graphSize.y;
    }

    static void SetLine(LineRenderer line, Vector3 start, Vector3 end)
    {
        if (line == null)
        {
            return;
        }

        line.positionCount = 2;
        line.SetPosition(0, start);
        line.SetPosition(1, end);
    }

    static void SetText(Text text, string value)
    {
        if (text != null)
        {
            text.text = value;
        }
    }
}
