using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class PIDArmSceneBuilder : MonoBehaviour
{
    const float PivotX = -1.25f;
    const float PivotY = 1.8f;
    const int UiWidth = 360;
    static readonly Vector3 GraphOrigin = new Vector3(-5.05f, -3.25f, 0f);
    static readonly Vector2 GraphSize = new Vector2(3.1f, 1.35f);

    static readonly Color BackgroundColor = new Color(0.95f, 0.96f, 0.97f);
    static readonly Color ArmColor = new Color(0.12f, 0.35f, 0.78f);
    static readonly Color TargetColor = new Color(0.1f, 0.65f, 0.35f, 0.45f);
    static readonly Color CurrentColor = new Color(0.95f, 0.45f, 0.05f);
    static readonly Color TrailColor = new Color(0.83f, 0.12f, 0.2f, 0.75f);
    static readonly Color GuideColor = new Color(0.25f, 0.28f, 0.34f, 0.35f);
    static readonly Color GraphLineColor = new Color(0.92f, 0.18f, 0.2f);

    PIDArmController controller;
    Font uiFont;
    Material lineMaterial;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (FindFirstObjectByType<PIDArmSceneBuilder>() != null)
        {
            return;
        }

        GameObject builder = new GameObject("PID Arm Scene Builder");
        builder.AddComponent<PIDArmSceneBuilder>();
    }

    void Awake()
    {
        Physics2D.gravity = new Vector2(0f, -9.81f);
        uiFont = Font.CreateDynamicFontFromOSFont(new[] { "Arial", "Yu Gothic", "Meiryo" }, 14);
        if (uiFont == null)
        {
            uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        if (uiFont == null)
        {
            uiFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        lineMaterial = new Material(Shader.Find("Sprites/Default"));

        CreateCamera();
        CreateEventSystem();
        BuildScene();
    }

    void CreateCamera()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            camera = cameraObject.AddComponent<Camera>();
        }

        camera.transform.position = new Vector3(0f, 0f, -10f);
        camera.orthographic = true;
        camera.orthographicSize = 4.2f;
        camera.backgroundColor = BackgroundColor;
    }

    void CreateEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<StandaloneInputModule>();
    }

    void BuildScene()
    {
        CreateBackgroundGuides();

        GameObject pivot = CreatePivot();
        GameObject arm = CreateArm(pivot.transform);
        GameObject tip = CreateTipMarker();

        LineRenderer armLine = CreateLine("Arm Body", ArmColor, 0.16f, 10);
        LineRenderer targetLine = CreateLine("Target Angle Guide", TargetColor, 0.045f, 3);
        LineRenderer currentLine = CreateLine("Current Angle Line", CurrentColor, 0.035f, 4);
        LineRenderer downLine = CreateLine("Down Reference Line", GuideColor, 0.025f, 1);
        LineRenderer trailLine = CreateLine("Tip Trail", TrailColor, 0.035f, 2);
        CreateAngleGraph(out LineRenderer angleGraphLine, out LineRenderer targetGraphLine);

        Canvas canvas = CreateCanvas();
        UiReferences ui = CreateControlPanel(canvas.transform);

        controller = gameObject.AddComponent<PIDArmController>();
        controller.Configure(
            arm.GetComponent<Rigidbody2D>(),
            arm.GetComponent<BoxCollider2D>(),
            arm.GetComponent<HingeJoint2D>(),
            pivot.transform,
            tip.transform,
            armLine,
            targetLine,
            currentLine,
            downLine,
            trailLine,
            angleGraphLine,
            targetGraphLine,
            GraphOrigin,
            GraphSize,
            ui.CurrentAngle,
            ui.TargetAngle,
            ui.Output,
            ui.Error,
            ui.PTerm,
            ui.ITerm,
            ui.DTerm,
            ui.PGain,
            ui.IGain,
            ui.DGain,
            ui.ArmLength,
            ui.ArmMass,
            ui.MaxTorque,
            ui.DampingCoefficient,
            ui.Convergence,
            ui.Overshoot);

        ConnectUi(ui);
    }

    GameObject CreatePivot()
    {
        GameObject pivot = new GameObject("Fixed Pivot");
        pivot.transform.position = new Vector3(PivotX, PivotY, 0f);

        Rigidbody2D pivotBody = pivot.AddComponent<Rigidbody2D>();
        pivotBody.bodyType = RigidbodyType2D.Static;

        SpriteRenderer renderer = pivot.AddComponent<SpriteRenderer>();
        renderer.sprite = CreateCircleSprite(64);
        renderer.color = new Color(0.08f, 0.09f, 0.1f);
        renderer.sortingOrder = 20;
        pivot.transform.localScale = Vector3.one * 0.22f;

        return pivot;
    }

    GameObject CreateArm(Transform pivot)
    {
        GameObject arm = new GameObject("Single Link Arm");
        arm.transform.position = pivot.position;
        arm.transform.rotation = Quaternion.identity;

        Rigidbody2D body = arm.AddComponent<Rigidbody2D>();
        body.gravityScale = 1f;
        body.mass = PIDArmController.DefaultArmMass;
        body.angularDamping = 0f;
        body.linearDamping = 0f;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;

        BoxCollider2D collider = arm.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(0.18f, PIDArmController.DefaultArmLength);
        collider.offset = new Vector2(0f, -PIDArmController.DefaultArmLength * 0.5f);

        HingeJoint2D joint = arm.AddComponent<HingeJoint2D>();
        joint.connectedBody = pivot.GetComponent<Rigidbody2D>();
        joint.autoConfigureConnectedAnchor = false;
        joint.anchor = Vector2.zero;
        joint.connectedAnchor = Vector2.zero;
        joint.enableCollision = false;
        joint.useLimits = false;
        joint.useMotor = false;

        return arm;
    }

    GameObject CreateTipMarker()
    {
        GameObject tip = new GameObject("Arm Tip Marker");
        SpriteRenderer renderer = tip.AddComponent<SpriteRenderer>();
        renderer.sprite = CreateCircleSprite(64);
        renderer.color = new Color(0.9f, 0.15f, 0.12f);
        renderer.sortingOrder = 25;
        tip.transform.localScale = Vector3.one * 0.16f;
        return tip;
    }

    Canvas CreateCanvas()
    {
        GameObject canvasObject = new GameObject("Canvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasObject.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1280f, 720f);
        canvasObject.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    UiReferences CreateControlPanel(Transform canvas)
    {
        GameObject panel = CreateUiObject("Control Panel", canvas);
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(1f, 1f, 1f, 0.88f);

        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.sizeDelta = new Vector2(UiWidth, 0f);
        rect.anchoredPosition = Vector2.zero;

        VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(14, 14, 12, 12);
        layout.spacing = 5f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        UiReferences ui = new UiReferences();
        AddHeader(panel.transform, "PID Pendulum Arm");

        ui.PSlider = AddSliderRow(panel.transform, "P gain", 0f, 100f, PIDArmController.DefaultPGain, out ui.PGain);
        ui.ISlider = AddSliderRow(panel.transform, "I gain", 0f, 20f, PIDArmController.DefaultIGain, out ui.IGain);
        ui.DSlider = AddSliderRow(panel.transform, "D gain", 0f, 30f, PIDArmController.DefaultDGain, out ui.DGain);
        ui.TargetSlider = AddSliderRow(panel.transform, "Target angle", 0f, 180f, PIDArmController.DefaultTargetAngle, out ui.TargetAngle);
        ui.LengthSlider = AddSliderRow(panel.transform, "Arm length", 0.5f, 4f, PIDArmController.DefaultArmLength, out ui.ArmLength);
        ui.MassSlider = AddSliderRow(panel.transform, "Arm mass", 0.1f, 5f, PIDArmController.DefaultArmMass, out ui.ArmMass);
        ui.MaxTorqueSlider = AddSliderRow(panel.transform, "Max torque", 10f, 300f, PIDArmController.DefaultMaxTorque, out ui.MaxTorque);
        ui.DampingSlider = AddSliderRow(panel.transform, "Damping", 0f, 3f, PIDArmController.DefaultDampingCoefficient, out ui.DampingCoefficient);

        GameObject buttonRow = CreateHorizontalRow("Button Row", panel.transform, 40f);
        ui.StartButton = AddButton(buttonRow.transform, "Start");
        ui.ResetButton = AddButton(buttonRow.transform, "Reset");

        AddSeparator(panel.transform);
        ui.CurrentAngle = AddReadout(panel.transform, "Current angle: 0.0 deg");
        ui.Error = AddReadout(panel.transform, "Error        : 0.0 deg");
        ui.Output = AddReadout(panel.transform, "Applied tq   : 0.0");
        ui.PTerm = AddReadout(panel.transform, "P term       : 0.0");
        ui.ITerm = AddReadout(panel.transform, "I term       : 0.0");
        ui.DTerm = AddReadout(panel.transform, "D term       : 0.0");
        ui.Overshoot = AddReadout(panel.transform, "Overshoot: none");
        ui.Convergence = AddReadout(panel.transform, "State: \u672a\u53ce\u675f");

        return ui;
    }

    void ConnectUi(UiReferences ui)
    {
        ui.PSlider.onValueChanged.AddListener(controller.SetPGain);
        ui.ISlider.onValueChanged.AddListener(controller.SetIGain);
        ui.DSlider.onValueChanged.AddListener(controller.SetDGain);
        ui.TargetSlider.onValueChanged.AddListener(controller.SetTargetAngle);
        ui.LengthSlider.onValueChanged.AddListener(controller.SetArmLength);
        ui.MassSlider.onValueChanged.AddListener(controller.SetArmMass);
        ui.MaxTorqueSlider.onValueChanged.AddListener(controller.SetMaxTorque);
        ui.DampingSlider.onValueChanged.AddListener(controller.SetDampingCoefficient);
        ui.StartButton.onClick.AddListener(controller.StartControl);
        ui.ResetButton.onClick.AddListener(controller.ResetSimulation);

        controller.SetPGain(ui.PSlider.value);
        controller.SetIGain(ui.ISlider.value);
        controller.SetDGain(ui.DSlider.value);
        controller.SetTargetAngle(ui.TargetSlider.value);
        controller.SetArmLength(ui.LengthSlider.value);
        controller.SetArmMass(ui.MassSlider.value);
        controller.SetMaxTorque(ui.MaxTorqueSlider.value);
        controller.SetDampingCoefficient(ui.DampingSlider.value);
    }

    void CreateBackgroundGuides()
    {
        for (int i = -5; i <= 5; i++)
        {
            LineRenderer vertical = CreateLine("Grid Vertical", new Color(0.25f, 0.28f, 0.32f, 0.12f), 0.01f, -10);
            vertical.positionCount = 2;
            vertical.SetPosition(0, new Vector3(i, -3.6f, 0f));
            vertical.SetPosition(1, new Vector3(i, 3.6f, 0f));

            LineRenderer horizontal = CreateLine("Grid Horizontal", new Color(0.25f, 0.28f, 0.32f, 0.12f), 0.01f, -10);
            horizontal.positionCount = 2;
            horizontal.SetPosition(0, new Vector3(-5.6f, i, 0f));
            horizontal.SetPosition(1, new Vector3(3.5f, i, 0f));
        }

        Vector3 pivot = new Vector3(PivotX, PivotY, 0f);
        for (int angle = 0; angle <= 180; angle += 15)
        {
            float radians = angle * Mathf.Deg2Rad;
            Vector3 direction = new Vector3(Mathf.Sin(radians), -Mathf.Cos(radians), 0f);
            Vector3 inner = pivot + direction * 2.35f;
            Vector3 outer = pivot + direction * (angle % 30 == 0 ? 2.62f : 2.5f);

            LineRenderer tick = CreateLine("Angle Tick", GuideColor, angle % 30 == 0 ? 0.025f : 0.015f, -2);
            tick.positionCount = 2;
            tick.SetPosition(0, inner);
            tick.SetPosition(1, outer);
        }
    }

    void CreateAngleGraph(out LineRenderer angleGraphLine, out LineRenderer targetGraphLine)
    {
        CreateGraphFrame();
        CreateWorldLabel("Angle Graph Label", "Angle vs Time", GraphOrigin + new Vector3(0f, GraphSize.y + 0.14f, 0f), 0.16f, TextAnchor.LowerLeft);
        CreateWorldLabel("Graph Top Label", "180 deg", GraphOrigin + new Vector3(-0.52f, GraphSize.y - 0.04f, 0f), 0.11f, TextAnchor.MiddleLeft);
        CreateWorldLabel("Graph Mid Label", "90 deg", GraphOrigin + new Vector3(-0.45f, GraphSize.y * 0.5f - 0.03f, 0f), 0.11f, TextAnchor.MiddleLeft);
        CreateWorldLabel("Graph Bottom Label", "0 deg", GraphOrigin + new Vector3(-0.42f, -0.04f, 0f), 0.11f, TextAnchor.MiddleLeft);
        CreateWorldLabel("Graph Time Label", "latest 10 s", GraphOrigin + new Vector3(GraphSize.x - 0.8f, -0.24f, 0f), 0.11f, TextAnchor.MiddleLeft);

        targetGraphLine = CreateLine("Target Angle Graph Line", TargetColor, 0.025f, 6);
        angleGraphLine = CreateLine("Current Angle Graph Line", GraphLineColor, 0.035f, 7);
    }

    void CreateGraphFrame()
    {
        Color frameColor = new Color(0.08f, 0.09f, 0.1f, 0.65f);
        Color gridColor = new Color(0.08f, 0.09f, 0.1f, 0.2f);

        CreateStaticLine("Graph Bottom Axis", GraphOrigin, GraphOrigin + new Vector3(GraphSize.x, 0f, 0f), frameColor, 0.02f, 5);
        CreateStaticLine("Graph Top Axis", GraphOrigin + new Vector3(0f, GraphSize.y, 0f), GraphOrigin + new Vector3(GraphSize.x, GraphSize.y, 0f), frameColor, 0.02f, 5);
        CreateStaticLine("Graph Left Axis", GraphOrigin, GraphOrigin + new Vector3(0f, GraphSize.y, 0f), frameColor, 0.02f, 5);
        CreateStaticLine("Graph Right Axis", GraphOrigin + new Vector3(GraphSize.x, 0f, 0f), GraphOrigin + new Vector3(GraphSize.x, GraphSize.y, 0f), frameColor, 0.02f, 5);

        for (int i = 1; i < 4; i++)
        {
            float x = GraphOrigin.x + GraphSize.x * i / 4f;
            CreateStaticLine("Graph Vertical Grid", new Vector3(x, GraphOrigin.y, 0f), new Vector3(x, GraphOrigin.y + GraphSize.y, 0f), gridColor, 0.01f, 4);
        }

        for (int i = 1; i < 4; i++)
        {
            float y = GraphOrigin.y + GraphSize.y * i / 4f;
            CreateStaticLine("Graph Horizontal Grid", new Vector3(GraphOrigin.x, y, 0f), new Vector3(GraphOrigin.x + GraphSize.x, y, 0f), gridColor, 0.01f, 4);
        }
    }

    void CreateStaticLine(string name, Vector3 start, Vector3 end, Color color, float width, int sortingOrder)
    {
        LineRenderer line = CreateLine(name, color, width, sortingOrder);
        line.positionCount = 2;
        line.SetPosition(0, start);
        line.SetPosition(1, end);
    }

    LineRenderer CreateLine(string name, Color color, float width, int sortingOrder)
    {
        GameObject lineObject = new GameObject(name);
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.material = lineMaterial;
        line.startColor = color;
        line.endColor = color;
        line.startWidth = width;
        line.endWidth = width;
        line.numCapVertices = 8;
        line.numCornerVertices = 4;
        line.sortingOrder = sortingOrder;
        line.positionCount = 0;
        return line;
    }

    Slider AddSliderRow(Transform parent, string label, float min, float max, float value, out Text valueText)
    {
        GameObject row = CreateUiObject(label + " Row", parent);
        LayoutElement rowLayout = row.AddComponent<LayoutElement>();
        rowLayout.preferredHeight = 40f;

        VerticalLayoutGroup vertical = row.AddComponent<VerticalLayoutGroup>();
        vertical.spacing = 2f;
        vertical.childControlWidth = true;
        vertical.childControlHeight = true;
        vertical.childForceExpandWidth = true;
        vertical.childForceExpandHeight = false;

        valueText = AddReadout(row.transform, label + ": " + value.ToString("0.0"));
        valueText.fontSize = 14;

        GameObject sliderObject = CreateUiObject(label + " Slider", row.transform);
        Slider slider = sliderObject.AddComponent<Slider>();
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = value;

        RectTransform sliderRect = sliderObject.GetComponent<RectTransform>();
        sliderRect.sizeDelta = new Vector2(0f, 22f);
        sliderObject.AddComponent<LayoutElement>().preferredHeight = 18f;

        Image background = AddChildImage(sliderObject.transform, "Background", new Color(0.74f, 0.77f, 0.81f), new Vector2(0f, 0.35f), new Vector2(1f, 0.65f));
        Image fill = AddChildImage(sliderObject.transform, "Fill", new Color(0.18f, 0.42f, 0.82f), new Vector2(0f, 0.35f), new Vector2(1f, 0.65f));
        Image handle = AddChildImage(sliderObject.transform, "Handle", new Color(0.08f, 0.09f, 0.1f), new Vector2(0f, 0f), new Vector2(0f, 1f));

        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(16f, 18f);

        slider.targetGraphic = handle;
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handleRect;
        slider.direction = Slider.Direction.LeftToRight;
        return slider;
    }

    Button AddButton(Transform parent, string label)
    {
        GameObject buttonObject = CreateUiObject(label + " Button", parent);
        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.12f, 0.34f, 0.66f);

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;

        LayoutElement layout = buttonObject.AddComponent<LayoutElement>();
        layout.flexibleWidth = 1f;
        layout.preferredHeight = 32f;

        Text text = AddText(buttonObject.transform, label, 16, FontStyle.Bold, TextAnchor.MiddleCenter);
        text.color = Color.white;
        Stretch(text.rectTransform, Vector2.zero, Vector2.one);
        return button;
    }

    Text AddReadout(Transform parent, string initialText)
    {
        GameObject textObject = CreateUiObject(initialText, parent);
        Text text = textObject.AddComponent<Text>();
        text.font = uiFont;
        text.fontSize = 12;
        text.color = new Color(0.08f, 0.09f, 0.1f);
        text.alignment = TextAnchor.MiddleLeft;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Truncate;

        LayoutElement layout = textObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 17f;

        text.text = initialText;
        return text;
    }

    void AddHeader(Transform parent, string text)
    {
        Text header = AddReadout(parent, text);
        header.fontSize = 20;
        header.fontStyle = FontStyle.Bold;
        header.alignment = TextAnchor.MiddleCenter;
        header.GetComponent<LayoutElement>().preferredHeight = 28f;
    }

    void AddSeparator(Transform parent)
    {
        GameObject separator = CreateUiObject("Separator", parent);
        Image image = separator.AddComponent<Image>();
        image.color = new Color(0.08f, 0.09f, 0.1f, 0.2f);
        separator.AddComponent<LayoutElement>().preferredHeight = 1f;
    }

    GameObject CreateHorizontalRow(string name, Transform parent, float height)
    {
        GameObject row = CreateUiObject(name, parent);
        HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        row.AddComponent<LayoutElement>().preferredHeight = height;
        return row;
    }

    Image AddChildImage(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject child = CreateUiObject(name, parent);
        Image image = child.AddComponent<Image>();
        image.color = color;
        Stretch(image.rectTransform, anchorMin, anchorMax);
        return image;
    }

    Text AddText(Transform parent, string value, int size, FontStyle style, TextAnchor alignment)
    {
        GameObject textObject = CreateUiObject("Text", parent);
        Text text = textObject.AddComponent<Text>();
        text.font = uiFont;
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        return text;
    }

    TextMesh CreateWorldLabel(string name, string value, Vector3 position, float characterSize, TextAnchor alignment)
    {
        GameObject label = new GameObject(name);
        label.transform.position = position;

        TextMesh text = label.AddComponent<TextMesh>();
        text.text = value;
        text.font = uiFont;
        text.characterSize = characterSize;
        text.anchor = alignment;
        text.alignment = TextAlignment.Left;
        text.color = new Color(0.08f, 0.09f, 0.1f, 0.75f);

        MeshRenderer renderer = label.GetComponent<MeshRenderer>();
        renderer.sortingOrder = 8;
        if (uiFont != null)
        {
            renderer.material = uiFont.material;
        }

        return text;
    }

    static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.AddComponent<RectTransform>();
        return obj;
    }

    static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    Sprite CreateCircleSprite(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color clear = new Color(0f, 0f, 0f, 0f);
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.45f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                texture.SetPixel(x, y, distance <= radius ? Color.white : clear);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
    }

    sealed class UiReferences
    {
        public Slider PSlider;
        public Slider ISlider;
        public Slider DSlider;
        public Slider TargetSlider;
        public Slider LengthSlider;
        public Slider MassSlider;
        public Slider MaxTorqueSlider;
        public Slider DampingSlider;
        public Button StartButton;
        public Button ResetButton;
        public Text CurrentAngle;
        public Text TargetAngle;
        public Text Output;
        public Text Error;
        public Text PTerm;
        public Text ITerm;
        public Text DTerm;
        public Text PGain;
        public Text IGain;
        public Text DGain;
        public Text ArmLength;
        public Text ArmMass;
        public Text MaxTorque;
        public Text DampingCoefficient;
        public Text Convergence;
        public Text Overshoot;
    }
}
