using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class CadUIManager : MonoBehaviour
{
    private enum AppMode
    {
        Pointer,
        Component,
    }

    private AppMode currentMode = AppMode.Pointer;

    [Header("Fritzing Database")]
    public List<FritzingComponentAsset> componentAssets = new List<FritzingComponentAsset>();

    [Header("UI Styles")]
    public StyleSheet customStyles;

    // UI Elements
    private FritzingComponentAsset selectedTool;
    private UIDocument uiDocument;

    private VisualElement designCanvas;
    private VisualElement canvasViewport;
    private VisualElement layerBoards;
    private VisualElement layerWires;
    private VisualElement layerComponents;
    private VisualElement layerTooltips;

    private Label pinTooltip;
    private string hoveredPinFullId = ""; // Tracks live pin hovering

    // Top Bar
    private Button btnUndo,
        btnRedo,
        btnCopy,
        btnPaste,
        btnDelete;
    private DropdownField componentDropdown;
    private DropdownField wireColorDropdown;
    public Color currentWireColor = Color.green;

    // Inspector
    private VisualElement inspectorPanel;
    private Label inspectorTitle;
    private TextField valueInput;
    private Label liveStatsLabel; // Live Multimeter Display
    private VisualElement currentlySelectedVE;

    // Drag State
    private VisualElement draggedElement = null;
    private Vector2 dragOffset;
    private bool isDragging = false;
    private Vector2 canvasPosition = Vector2.zero;
    private float canvasZoom = 1.0f;
    private bool isPanningCanvas = false;
    private Vector2 panStartMousePos;
    private Vector2 panStartCanvasPos;

    // Wiring State
    private bool isDrawingWire = false;
    private WireElement activeWire = null;
    private List<Vector2> activeWirePoints = new List<Vector2>();

    private StyleColor colorBgDark = new StyleColor(new Color(0.12f, 0.12f, 0.12f));
    private StyleColor colorPanelBg = new StyleColor(new Color(0.16f, 0.16f, 0.16f));
    private StyleColor colorBorder = new StyleColor(new Color(0.24f, 0.24f, 0.25f));
    private StyleColor colorAccent = new StyleColor(new Color(0.0f, 0.48f, 0.8f));

    private void OnEnable()
    {
        uiDocument = GetComponent<UIDocument>();
        var root = uiDocument.rootVisualElement;
        if (root == null)
            return;
        root.Clear();
        if (customStyles != null)
            root.styleSheets.Add(customStyles);

        root.style.width = Length.Percent(100);
        root.style.height = Length.Percent(100);
        root.style.flexDirection = FlexDirection.Column;
        root.style.backgroundColor = colorBgDark;

        // TOP BAR
        var topBar = new VisualElement()
        {
            style =
            {
                height = 55,
                flexDirection = FlexDirection.Row,
                backgroundColor = colorPanelBg,
                alignItems = Align.Center,
                paddingLeft = 15,
                paddingRight = 15,
                borderBottomWidth = 1,
                borderBottomColor = colorBorder,
            },
        };

        btnCopy = CreateIconButton("📋", "Copy (Ctrl+C)");
        btnPaste = CreateIconButton("📄", "Paste (Ctrl+V)");
        btnDelete = CreateIconButton("🗑", "Delete (Backspace)");
        btnUndo = CreateIconButton("↶", "Undo (Ctrl+Z)");
        btnRedo = CreateIconButton("↷", "Redo (Ctrl+Y)");
        btnDelete.clicked += DeleteSelectedElement;
        btnCopy.SetEnabled(false);
        btnDelete.SetEnabled(false);

        topBar.Add(btnCopy);
        topBar.Add(btnPaste);
        topBar.Add(btnDelete);
        topBar.Add(
            new VisualElement()
            {
                style =
                {
                    width = 1,
                    height = 30,
                    backgroundColor = colorBorder,
                    marginLeft = 10,
                    marginRight = 10,
                },
            }
        );
        topBar.Add(btnUndo);
        topBar.Add(btnRedo);
        topBar.Add(
            new VisualElement()
            {
                style =
                {
                    width = 1,
                    height = 30,
                    backgroundColor = colorBorder,
                    marginLeft = 10,
                    marginRight = 10,
                },
            }
        );

        var colorLabel = new Label("Wire Color:")
        {
            style = { color = Color.white, marginRight = 5 },
        };
        topBar.Add(colorLabel);

        wireColorDropdown = new DropdownField(
            string.Empty,
            new List<string> { "Green", "Red", "Black", "Blue", "Yellow" },
            0
        )
        {
            style = { width = 100 },
        };
        var wireDropLabel = wireColorDropdown.Q<Label>();
        if (wireDropLabel != null)
            wireDropLabel.style.display = DisplayStyle.None;
        wireColorDropdown.RegisterValueChangedCallback(evt =>
        {
            switch (evt.newValue)
            {
                case "Red":
                    currentWireColor = Color.red;
                    break;
                case "Black":
                    currentWireColor = Color.black;
                    break;
                case "Blue":
                    currentWireColor = Color.blue;
                    break;
                case "Yellow":
                    currentWireColor = Color.yellow;
                    break;
                default:
                    currentWireColor = Color.green;
                    break;
            }
        });
        topBar.Add(wireColorDropdown);

        // --- THE LIVE SIMULATION TOGGLE ---
        var simBtn = CreateModernButton("▶ Play", true);
        simBtn.style.backgroundColor = new StyleColor(new Color(0.2f, 0.7f, 0.3f));
        simBtn.clicked += () =>
        {
            var bridge = Object.FindAnyObjectByType<SimulatorBridge>();
            if (bridge == null)
                return;

            bridge.ToggleSimulation();

            if (bridge.isRunning)
            {
                simBtn.text = "⬛ Stop";
                simBtn.style.backgroundColor = new StyleColor(new Color(0.8f, 0.2f, 0.2f));
            }
            else
            {
                simBtn.text = "▶ Play";
                simBtn.style.backgroundColor = new StyleColor(new Color(0.2f, 0.7f, 0.3f));
            }
        };
        topBar.Add(simBtn);
        // --- ADD RESET BUTTON ---
        var resetBtn = CreateModernButton("↺ Reset", false);
        resetBtn.clicked += () =>
        {
            var bridge = Object.FindAnyObjectByType<SimulatorBridge>();
            if (bridge != null)
            {
                bridge.ResetSimulation();
                liveStatsLabel.text = "Stats Reset";
            }
        };
        topBar.Add(resetBtn);
        topBar.Add(new VisualElement() { style = { flexGrow = 1 } });

        var compLabel = new Label("Components:")
        {
            style = { color = Color.white, marginRight = 10 },
        };
        topBar.Add(compLabel);

        List<string> compOptions = new List<string> { "--- Select ---" };
        compOptions.AddRange(componentAssets.Select(a => a.Name));
        componentDropdown = new DropdownField(string.Empty, compOptions, 0)
        {
            style = { width = 250 },
        };
        var dropLabel = componentDropdown.Q<Label>();
        if (dropLabel != null)
            dropLabel.style.display = DisplayStyle.None;
        componentDropdown.RegisterValueChangedCallback(evt =>
        {
            if (evt.newValue != "--- Select ---")
            {
                currentMode = AppMode.Component;
                selectedTool = componentAssets.FirstOrDefault(a => a.Name == evt.newValue);
                ClearSelection();
                if (isDrawingWire)
                    CancelWire();
            }
        });
        topBar.Add(componentDropdown);
        root.Add(topBar);

        // MAIN BODY
        var mainBody = new VisualElement()
        {
            style = { flexDirection = FlexDirection.Row, flexGrow = 1 },
        };
        var codeEditorPanel = new VisualElement()
        {
            style =
            {
                width = Length.Percent(25),
                minWidth = 250,
                backgroundColor = colorBgDark,
                borderRightWidth = 1,
                borderRightColor = colorBorder,
            },
        };
        var codeField = new TextField()
        {
            multiline = true,
            value = "// Write your AVR C++ code here...\nvoid setup() {\n\n}\n\nvoid loop() {\n\n}",
            style = { flexGrow = 1, color = Color.white },
        };
        codeField.RegisterCallback<KeyDownEvent>(
            evt =>
            {
                if (evt.keyCode == KeyCode.Escape)
                    evt.StopPropagation();
            },
            TrickleDown.TrickleDown
        );
        var textInput = codeField.Q("unity-text-input");
        textInput.style.backgroundColor = Color.clear;
        textInput.style.paddingTop = 15;
        textInput.style.paddingLeft = 15;
        textInput.style.borderTopWidth = 0;
        textInput.style.borderLeftWidth = 0;
        codeEditorPanel.Add(codeField);

        canvasViewport = new VisualElement()
        {
            style =
            {
                flexGrow = 1,
                minWidth = 300,
                overflow = Overflow.Hidden,
                backgroundColor = new StyleColor(new Color(0.14f, 0.14f, 0.14f)),
            },
        };
        designCanvas = new VisualElement()
        {
            style =
            {
                width = 0,
                height = 0,
                overflow = Overflow.Visible,
                transformOrigin = new TransformOrigin(0, 0),
            },
        };

        // LAYER STACK
        layerBoards = new VisualElement()
        {
            style = { position = Position.Absolute },
            pickingMode = PickingMode.Ignore,
        };
        layerWires = new VisualElement()
        {
            style = { position = Position.Absolute },
            pickingMode = PickingMode.Ignore,
        };
        layerComponents = new VisualElement()
        {
            style = { position = Position.Absolute },
            pickingMode = PickingMode.Ignore,
        };
        layerTooltips = new VisualElement()
        {
            style = { position = Position.Absolute },
            pickingMode = PickingMode.Ignore,
        };

        designCanvas.Add(layerBoards); // Bottom
        designCanvas.Add(layerWires); // Middle
        designCanvas.Add(layerComponents); // Top
        designCanvas.Add(layerTooltips); // Absolute Top

        pinTooltip = new Label();
        pinTooltip.style.position = Position.Absolute;
        pinTooltip.style.backgroundColor = new StyleColor(new Color(0.1f, 0.1f, 0.1f, 0.95f));
        pinTooltip.style.color = Color.white;
        pinTooltip.style.fontSize = 12;
        pinTooltip.style.unityFontStyleAndWeight = FontStyle.Bold;
        pinTooltip.style.paddingLeft = 6;
        pinTooltip.style.paddingRight = 6;
        pinTooltip.style.paddingTop = 4;
        pinTooltip.style.paddingBottom = 4;
        pinTooltip.style.borderTopLeftRadius = 4;
        pinTooltip.style.borderTopRightRadius = 4;
        pinTooltip.style.borderBottomLeftRadius = 4;
        pinTooltip.style.borderBottomRightRadius = 4;
        pinTooltip.style.borderTopWidth = 1;
        pinTooltip.style.borderBottomWidth = 1;
        pinTooltip.style.borderLeftWidth = 1;
        pinTooltip.style.borderRightWidth = 1;
        pinTooltip.style.borderTopColor = colorBorder;
        pinTooltip.style.borderBottomColor = colorBorder;
        pinTooltip.style.borderLeftColor = colorBorder;
        pinTooltip.style.borderRightColor = colorBorder;
        pinTooltip.style.display = DisplayStyle.None;
        layerTooltips.Add(pinTooltip);

        canvasViewport.Add(designCanvas);
        canvasViewport.RegisterCallback<PointerDownEvent>(OnCanvasClicked);
        canvasViewport.RegisterCallback<WheelEvent>(OnCanvasScroll);
        canvasViewport.RegisterCallback<PointerMoveEvent>(OnCanvasPointerMove);
        canvasViewport.RegisterCallback<PointerUpEvent>(OnCanvasPointerUp);

        // INSPECTOR
        inspectorPanel = new VisualElement()
        {
            style =
            {
                position = Position.Absolute,
                right = 20,
                top = 20,
                width = 250,
                height = StyleKeyword.Auto,
                backgroundColor = colorPanelBg,
                borderTopWidth = 1,
                borderBottomWidth = 1,
                borderLeftWidth = 1,
                borderRightWidth = 1,
                borderTopColor = colorBorder,
                borderBottomColor = colorBorder,
                borderLeftColor = colorBorder,
                borderRightColor = colorBorder,
                borderTopLeftRadius = 8,
                borderTopRightRadius = 8,
                borderBottomLeftRadius = 8,
                borderBottomRightRadius = 8,
                paddingTop = 15,
                paddingBottom = 15,
                paddingLeft = 15,
                paddingRight = 15,
                display = DisplayStyle.None,
            },
        };
        var inspectorHeader = new VisualElement()
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                justifyContent = Justify.SpaceBetween,
                marginBottom = 15,
            },
        };
        inspectorTitle = new Label("Properties")
        {
            style =
            {
                color = Color.white,
                fontSize = 16,
                unityFontStyleAndWeight = FontStyle.Bold,
                whiteSpace = WhiteSpace.Normal,
            },
        };
        var closeBtn = new Button(() => ClearSelection())
        {
            text = "✖",
            style =
            {
                backgroundColor = Color.clear,
                color = Color.gray,
                borderTopWidth = 0,
                borderBottomWidth = 0,
                borderLeftWidth = 0,
                borderRightWidth = 0,
            },
        };
        inspectorHeader.Add(inspectorTitle);
        inspectorHeader.Add(closeBtn);

        valueInput = new TextField("Value:");
        var valLabel = valueInput.Q<Label>();
        if (valLabel != null)
        {
            valLabel.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f));
            valLabel.style.minWidth = 50;
        }
        var valInputArea = valueInput.Q("unity-text-input");
        valInputArea.style.color = Color.white;
        valInputArea.style.backgroundColor = colorBgDark;
        valInputArea.style.borderBottomColor = colorAccent;
        valInputArea.style.borderBottomWidth = 2;
        valInputArea.style.borderTopWidth = 0;
        valInputArea.style.borderLeftWidth = 0;
        valInputArea.style.borderRightWidth = 0;

        valueInput.RegisterValueChangedCallback(evt =>
        {
            if (currentlySelectedVE != null && double.TryParse(evt.newValue, out double newVal))
            {
                var data = currentlySelectedVE.userData as PlacedComponentData;
                if (data != null)
                {
                    data.componentValue = newVal;
                    // Trigger Live Math Recompile on value change!
                    var bridge = Object.FindAnyObjectByType<SimulatorBridge>();
                    if (bridge != null && bridge.isRunning)
                        bridge.PushToEngine();
                }
            }
        });

        // LIVE MULTIMETER LABELS
        liveStatsLabel = new Label("Live Stats: --");
        liveStatsLabel.style.color = new StyleColor(new Color(0.3f, 0.8f, 1f));
        liveStatsLabel.style.marginTop = 15;
        liveStatsLabel.style.fontSize = 14;
        liveStatsLabel.style.unityFontStyleAndWeight = FontStyle.Bold;

        inspectorPanel.Add(inspectorHeader);
        inspectorPanel.Add(valueInput);
        inspectorPanel.Add(liveStatsLabel); // Bottom slot

        canvasViewport.Add(inspectorPanel);
        mainBody.Add(codeEditorPanel);
        mainBody.Add(canvasViewport);
        root.Add(mainBody);
    }

    private Button CreateIconButton(string icon, string tooltip)
    {
        return new Button()
        {
            text = icon,
            tooltip = tooltip,
            style =
            {
                width = 40,
                height = 35,
                fontSize = 16,
                borderTopLeftRadius = 6,
                borderTopRightRadius = 6,
                borderBottomLeftRadius = 6,
                borderBottomRightRadius = 6,
                borderTopWidth = 0,
                borderBottomWidth = 0,
                borderLeftWidth = 0,
                borderRightWidth = 0,
                marginLeft = 2,
                marginRight = 2,
                backgroundColor = Color.clear,
                color = Color.white,
            },
        };
    }

    private void ClearSelection()
    {
        if (currentlySelectedVE is WireElement wire)
        {
            wire.isSelected = false;
            wire.MarkDirtyRepaint();
        }
        currentlySelectedVE = null;
        inspectorPanel.style.display = DisplayStyle.None;
        btnDelete.SetEnabled(false);
        btnCopy.SetEnabled(false);
    }

    private void CancelWire()
    {
        if (isDrawingWire && activeWire != null)
        {
            if (activeWire.parent != null)
                activeWire.parent.Remove(activeWire);
            isDrawingWire = false;
            activeWire = null;
            activeWirePoints.Clear();
        }
    }

    private void DeleteSelectedElement()
    {
        if (currentlySelectedVE != null)
        {
            if (currentlySelectedVE is WireElement wireToKill)
            {
                var bridge = Object.FindAnyObjectByType<SimulatorBridge>();
                if (bridge != null)
                    bridge.UnregisterWire(wireToKill);
            }
            if (currentlySelectedVE.parent != null)
                currentlySelectedVE.parent.Remove(currentlySelectedVE);
            ClearSelection();

            // Trigger Live Math Recompile on delete!
            var liveBridge = Object.FindAnyObjectByType<SimulatorBridge>();
            if (liveBridge != null && liveBridge.isRunning)
                liveBridge.PushToEngine();
        }
    }

    private Vector2 GetSnappedPosition(Vector2 mouseLocalPos)
    {
        float snapRadius = 15f;
        Vector2 bestSnapPos = mouseLocalPos;
        float closestDistance = float.MaxValue;
        var allPins = designCanvas.Query<VisualElement>(className: "snap-node").ToList();
        foreach (var pin in allPins)
        {
            float compX = pin.parent.style.left.value.value;
            float compY = pin.parent.style.top.value.value;
            float pinX = pin.style.left.value.value;
            float pinY = pin.style.top.value.value;

            // Fix: DYNAMIC SIZING FOR SNAP POINTS
            float pW = pin.style.width.value.value;
            float pH = pin.style.height.value.value;
            Vector2 exactPinCanvasPos = new Vector2(
                compX + pinX + (pW / 2f),
                compY + pinY + (pH / 2f)
            );

            float distance = Vector2.Distance(mouseLocalPos, exactPinCanvasPos);
            if (distance < snapRadius && distance < closestDistance)
            {
                closestDistance = distance;
                bestSnapPos = exactPinCanvasPos;
            }
        }
        return bestSnapPos;
    }

    private void OnCanvasClicked(PointerDownEvent evt)
    {
        if (isDragging || evt.button != 0)
            return;
        Vector2 localPos = designCanvas.WorldToLocal(evt.position);

        // --- THE WIRE SELECTOR (Raycast math over custom painters) ---
        if (currentMode == AppMode.Pointer && !isDrawingWire)
        {
            var allWires = layerWires.Query<WireElement>().ToList();
            foreach (var wire in allWires)
            {
                if (wire.ContainsPoint(wire.WorldToLocal(evt.position)))
                {
                    SetSelectedElement(wire, "Wire");
                    evt.StopPropagation();
                    return;
                }
            }
        }

        if (isDrawingWire && (evt.target == designCanvas || evt.target == canvasViewport))
        {
            activeWirePoints[activeWirePoints.Count - 1] = localPos;
            activeWirePoints.Add(localPos);
            activeWire.UpdateWire(activeWirePoints);
            return;
        }

        if (
            currentMode == AppMode.Pointer
            && (evt.target == designCanvas || evt.target == canvasViewport)
        )
        {
            ClearSelection();
        }

        if (currentMode == AppMode.Component && selectedTool != null)
        {
            if (evt.target != null && evt.target != designCanvas && evt.target != canvasViewport)
                return;

            var componentVE = new VisualElement();
            float compWidth = selectedTool.realWorldWidth;
            float compHeight = selectedTool.realWorldHeight;

            if (selectedTool.icon != null)
            {
                componentVE.style.backgroundImage = new StyleBackground(selectedTool.icon);
                componentVE.style.backgroundSize = new StyleBackgroundSize(
                    new BackgroundSize(BackgroundSizeType.Contain)
                );
                componentVE.style.backgroundRepeat = new StyleBackgroundRepeat(
                    new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat)
                );
            }

            componentVE.style.width = compWidth;
            componentVE.style.height = compHeight;
            componentVE.style.position = Position.Absolute;
            componentVE.style.left = localPos.x - (compWidth / 2f);
            componentVE.style.top = localPos.y - (compHeight / 2f);

            foreach (var pin in selectedTool.pins)
            {
                var pinNode = new VisualElement() { userData = pin.pinName }; // Backend internal ID
                pinNode.style.position = Position.Absolute;

                // Set custom size from Inspector (fallback to 10 if missing)
                float pw = pin.pinWidth > 0 ? pin.pinWidth : 10f;
                float ph = pin.pinHeight > 0 ? pin.pinHeight : 10f;
                pinNode.style.width = pw;
                pinNode.style.height = ph;
                pinNode.style.left = (pin.cx * compWidth) - (pw / 2f);
                pinNode.style.top = (pin.cy * compHeight) - (ph / 2f);
                pinNode.style.backgroundColor = Color.clear;
                pinNode.AddToClassList("snap-node");

                pinNode.RegisterCallback<PointerEnterEvent>(e =>
                {
                    if (currentMode == AppMode.Pointer)
                    {
                        pinNode.style.backgroundColor = new StyleColor(new Color(1f, 0f, 0f, 0.7f));

                        var compData = componentVE.userData as PlacedComponentData;
                        hoveredPinFullId = $"{compData.instanceId}_{(string)pinNode.userData}";
                        pinTooltip.userData = pin.name; // Save Frontend Name
                        pinTooltip.text = pin.name;

                        float cX = componentVE.style.left.value.value;
                        float cY = componentVE.style.top.value.value;
                        float pX = pinNode.style.left.value.value;
                        float pY = pinNode.style.top.value.value;
                        pinTooltip.style.left = cX + pX + 15f;
                        pinTooltip.style.top = cY + pY - 25f;
                        pinTooltip.style.display = DisplayStyle.Flex;
                    }
                });

                pinNode.RegisterCallback<PointerLeaveEvent>(e =>
                {
                    pinNode.style.backgroundColor = Color.clear;
                    pinTooltip.style.display = DisplayStyle.None;
                    hoveredPinFullId = "";
                });

                pinNode.RegisterCallback<PointerDownEvent>(e =>
                {
                    if (currentMode == AppMode.Pointer && e.button == 0)
                    {
                        float cX = componentVE.style.left.value.value;
                        float cY = componentVE.style.top.value.value;
                        float pX = pinNode.style.left.value.value;
                        float pY = pinNode.style.top.value.value;
                        float pW = pinNode.style.width.value.value;
                        float pH = pinNode.style.height.value.value;
                        Vector2 exactPos = new Vector2(cX + pX + (pW / 2f), cY + pY + (pH / 2f));

                        var compData = componentVE.userData as PlacedComponentData;
                        string clickedPinId = pinNode.userData as string;

                        if (!isDrawingWire)
                        {
                            isDrawingWire = true;
                            pinTooltip.style.display = DisplayStyle.None;
                            activeWirePoints.Clear();
                            activeWirePoints.Add(exactPos);
                            activeWirePoints.Add(exactPos);

                            activeWire = new WireElement();
                            activeWire.wireColor = currentWireColor;
                            activeWire.startComponent = compData;
                            activeWire.startPinId = clickedPinId;
                            activeWire.UpdateWire(activeWirePoints);
                            layerWires.Add(activeWire);
                        }
                        else
                        {
                            if (Vector2.Distance(activeWirePoints[0], exactPos) > 0.1f)
                            {
                                activeWirePoints[activeWirePoints.Count - 1] = exactPos;
                                activeWire.endComponent = compData;
                                activeWire.endPinId = clickedPinId;
                                activeWire.UpdateWire(activeWirePoints);

                                var bridge = Object.FindAnyObjectByType<SimulatorBridge>();
                                if (bridge != null)
                                    bridge.RegisterWire(activeWire);
                                FinalizeWire(activeWire);
                            }
                            else
                            {
                                CancelWire();
                            }
                        }
                        e.StopPropagation();
                    }
                });
                componentVE.Add(pinNode);
            }

            componentVE.userData = new PlacedComponentData
            {
                asset = selectedTool,
                componentValue = 1000,
            };
            componentVE.RegisterCallback<PointerDownEvent>(e =>
                OnComponentPointerDown(e, componentVE)
            );
            componentVE.RegisterCallback<PointerMoveEvent>(e =>
                OnComponentPointerMove(e, componentVE)
            );
            componentVE.RegisterCallback<PointerUpEvent>(e => OnComponentPointerUp(e, componentVE));

            if (selectedTool.Name.Contains("Arduino") || selectedTool.Name.Contains("Breadboard"))
                layerBoards.Add(componentVE);
            else
                layerComponents.Add(componentVE);

            SetSelectedElement(componentVE, "Component");
            currentMode = AppMode.Pointer;
            selectedTool = null;
            componentDropdown.SetValueWithoutNotify("--- Select ---");
        }
    }

    private void FinalizeWire(WireElement wire)
    {
        isDrawingWire = false;
        activeWire = null;
        activeWirePoints.Clear();
        wire.RegisterCallback<PointerDownEvent>(e =>
        {
            if (currentMode == AppMode.Pointer && e.button == 0)
            {
                SetSelectedElement(wire, "Wire");
                e.StopPropagation();
            }
        });
    }

    private void SetSelectedElement(VisualElement element, string elementType)
    {
        ClearSelection();
        currentlySelectedVE = element;
        if (elementType == "Wire")
        {
            var wire = element as WireElement;
            wire.isSelected = true;
            wire.MarkDirtyRepaint();
            inspectorTitle.text = "Routed Wire";
            valueInput.style.display = DisplayStyle.None;
            btnCopy.SetEnabled(false);
        }
        else
        {
            var data = element.userData as PlacedComponentData;
            inspectorTitle.text = $"{data.asset.Name}";
            valueInput.style.display = DisplayStyle.Flex;
            valueInput.SetValueWithoutNotify(data.componentValue.ToString());
            btnCopy.SetEnabled(true);
        }
        inspectorPanel.style.display = DisplayStyle.Flex;
        btnDelete.SetEnabled(true);
    }

    private void OnComponentPointerDown(PointerDownEvent evt, VisualElement component)
    {
        if (isDrawingWire)
        {
            CancelWire();
            evt.StopPropagation();
            return;
        }
        component.BringToFront();
        isDragging = true;
        draggedElement = component;
        dragOffset = evt.localPosition;
        component.CapturePointer(evt.pointerId);
        evt.StopPropagation();
        SetSelectedElement(component, "Component");
    }

    private void OnComponentPointerMove(PointerMoveEvent evt, VisualElement component)
    {
        if (
            !isDragging
            || draggedElement != component
            || !component.HasPointerCapture(evt.pointerId)
        )
            return;
        Vector2 canvasPos = designCanvas.WorldToLocal(evt.position);
        component.style.left = canvasPos.x - dragOffset.x;
        component.style.top = canvasPos.y - dragOffset.y;

        var compData = component.userData as PlacedComponentData;
        if (compData != null)
        {
            var allWires = designCanvas.Query<WireElement>().ToList();
            foreach (var wire in allWires)
            {
                bool wireChanged = false;
                if (
                    wire.startComponent != null
                    && wire.startComponent.instanceId == compData.instanceId
                )
                {
                    wire.points[0] = GetExactPinPosition(component, wire.startPinId);
                    wireChanged = true;
                }
                if (
                    wire.endComponent != null
                    && wire.endComponent.instanceId == compData.instanceId
                )
                {
                    wire.points[wire.points.Count - 1] = GetExactPinPosition(
                        component,
                        wire.endPinId
                    );
                    wireChanged = true;
                }
                if (wireChanged)
                    wire.UpdateWire(wire.points);
            }
        }
    }

    private void OnComponentPointerUp(PointerUpEvent evt, VisualElement component)
    {
        if (isDragging && draggedElement == component)
        {
            isDragging = false;
            draggedElement = null;
            component.ReleasePointer(evt.pointerId);
        }
    }

    private void OnCanvasScroll(WheelEvent evt)
    {
        float zoomDelta = evt.delta.y > 0 ? 0.9f : 1.1f;
        canvasZoom = Mathf.Clamp(canvasZoom * zoomDelta, 0.2f, 5.0f);
        designCanvas.style.scale = new StyleScale(new Vector2(canvasZoom, canvasZoom));
        evt.StopPropagation();
    }

    private void OnCanvasPointerMove(PointerMoveEvent evt)
    {
        if (evt.pressedButtons == 4)
        {
            if (!isPanningCanvas)
            {
                isPanningCanvas = true;
                panStartMousePos = evt.position;
                panStartCanvasPos = canvasPosition;
            }
            Vector2 delta = (Vector2)evt.position - panStartMousePos;
            canvasPosition = panStartCanvasPos + delta;
            designCanvas.style.translate = new StyleTranslate(
                new Translate(canvasPosition.x, canvasPosition.y, 0)
            );
            evt.StopPropagation();
        }
        else
        {
            isPanningCanvas = false;
        }
        if (isDrawingWire && activeWire != null)
        {
            Vector2 localPos = designCanvas.WorldToLocal(evt.position);
            Vector2 snappedPos = GetSnappedPosition(localPos);
            activeWirePoints[activeWirePoints.Count - 1] = snappedPos;
            activeWire.UpdateWire(activeWirePoints);
        }
    }

    private void OnCanvasPointerUp(PointerUpEvent evt)
    {
        isPanningCanvas = false;
    }

    private Vector2 GetExactPinPosition(VisualElement component, string pinId)
    {
        var pinNode = component
            .Query<VisualElement>(className: "snap-node")
            .Where(p => (string)p.userData == pinId)
            .First();
        float cX = component.style.left.value.value;
        float cY = component.style.top.value.value;
        float pX = pinNode.style.left.value.value;
        float pY = pinNode.style.top.value.value;
        float pW = pinNode.style.width.value.value;
        float pH = pinNode.style.height.value.value;
        return new Vector2(cX + pX + (pW / 2f), cY + pY + (pH / 2f));
    }

    private Button CreateModernButton(string text, bool isActive)
    {
        var btn = new Button() { text = text };
        btn.style.height = 35;
        btn.style.borderTopLeftRadius = 6;
        btn.style.borderTopRightRadius = 6;
        btn.style.borderBottomLeftRadius = 6;
        btn.style.borderBottomRightRadius = 6;
        btn.style.borderTopWidth = 0;
        btn.style.borderBottomWidth = 0;
        btn.style.borderLeftWidth = 0;
        btn.style.borderRightWidth = 0;
        btn.style.marginLeft = 5;
        if (isActive)
        {
            btn.style.backgroundColor = colorAccent;
            btn.style.color = Color.white;
            btn.style.unityFontStyleAndWeight = FontStyle.Bold;
        }
        else
        {
            btn.style.backgroundColor = colorBgDark;
            btn.style.color = Color.gray;
        }
        return btn;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isDrawingWire)
                CancelWire();
            else
                ClearSelection();
        }
        if (Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace))
        {
            var focusController = designCanvas?.panel?.focusController;
            var focusedElement = focusController?.focusedElement as VisualElement;
            if (
                focusedElement == null
                || !(focusedElement is TextField || focusedElement.parent is TextField)
            )
                DeleteSelectedElement();
        }

        var bridge = Object.FindAnyObjectByType<SimulatorBridge>();
        if (bridge != null && bridge.isRunning)
        {
            // --- 1. LIVE TOOLTIP UPDATES ---
            if (hoveredPinFullId != "" && pinTooltip.style.display == DisplayStyle.Flex)
            {
                if (bridge.nodeToNetId.TryGetValue(hoveredPinFullId, out int netId))
                {
                    if (bridge.liveVoltages.TryGetValue(netId, out double volts))
                    {
                        string baseName = pinTooltip.userData as string ?? "Pin";
                        pinTooltip.text = $"{baseName} | {volts:F2}V";
                    }
                }
            }

            // --- 2. COMPONENT VISUAL STATES (GLOWING LEDS) ---
            foreach (var ve in layerComponents.Children())
            {
                var comp = ve.userData as PlacedComponentData;
                if (comp == null || comp.asset == null)
                    continue;

                string name = comp.asset.Name;
                if (name.Contains("LED") || name.Contains("OLED"))
                {
                    string p0Id = $"{comp.instanceId}_connector0pin";
                    string p1Id = $"{comp.instanceId}_connector1pin";
                    int net0 = bridge.nodeToNetId.ContainsKey(p0Id) ? bridge.nodeToNetId[p0Id] : -1;
                    int net1 = bridge.nodeToNetId.ContainsKey(p1Id) ? bridge.nodeToNetId[p1Id] : -1;

                    if (net0 != -1 && net1 != -1)
                    {
                        double v0 = bridge.liveVoltages.ContainsKey(net0)
                            ? bridge.liveVoltages[net0]
                            : 0;
                        double v1 = bridge.liveVoltages.ContainsKey(net1)
                            ? bridge.liveVoltages[net1]
                            : 0;
                        double voltageDrop = System.Math.Abs(v1 - v0);

                        double turnOnThreshold = name.Contains("OLED") ? 2.5 : 1.8;
                        if (voltageDrop >= turnOnThreshold)
                            ve.style.unityBackgroundImageTintColor = new StyleColor(
                                name.Contains("OLED") ? Color.cyan : Color.red
                            );
                        else
                            ve.style.unityBackgroundImageTintColor = new StyleColor(Color.white);
                    }
                }
            }

            if (
                currentlySelectedVE != null
                && currentlySelectedVE.userData is PlacedComponentData selectedComp
            )
            {
                string p0Id = $"{selectedComp.instanceId}_connector0pin";
                string p1Id = $"{selectedComp.instanceId}_connector1pin";
                int net0 = bridge.nodeToNetId.ContainsKey(p0Id) ? bridge.nodeToNetId[p0Id] : -1;
                int net1 = bridge.nodeToNetId.ContainsKey(p1Id) ? bridge.nodeToNetId[p1Id] : -1;

                double v0 =
                    (net0 != -1 && bridge.liveVoltages.ContainsKey(net0))
                        ? bridge.liveVoltages[net0]
                        : 0;
                double v1 =
                    (net1 != -1 && bridge.liveVoltages.ContainsKey(net1))
                        ? bridge.liveVoltages[net1]
                        : 0;

                double voltageDrop = System.Math.Abs(v1 - v0);
                double amps = 0;
                string compName = selectedComp.asset.Name;

                if (compName.Contains("Battery") || compName.Contains("18650"))
                {
                    // --- NEW: BATTERY CAPACITY DISPLAY ---
                    var bat = bridge.liveBatteries.FirstOrDefault(b =>
                        b.instanceId == selectedComp.instanceId
                    );
                    if (
                        bat != null
                        && bridge.liveCurrents != null
                        && bridge.liveCurrents.Length > bat.sourceIndex
                    )
                    {
                        amps = System.Math.Abs(bridge.liveCurrents[bat.sourceIndex]);
                        string currentText = amps < 1.0 ? $"{(amps * 1000):F2} mA" : $"{amps:F2} A";

                        // Show Fuel Remaining instead of Voltage Drop!
                        liveStatsLabel.text =
                            $"Capacity: {bat.current_mAh:F0} / {bat.max_mAh:F0} mAh\nOutput: {currentText}";
                    }
                }
                else
                {
                    // --- STANDARD COMPONENT DISPLAY (Resistors, LEDs, etc.) ---
                    if (compName.Contains("Resistor"))
                    {
                        amps = voltageDrop / selectedComp.componentValue;
                    }
                    else if (compName.Contains("LED") || compName.Contains("OLED"))
                    {
                        amps = voltageDrop / 150.0;
                    }

                    string currentText = amps < 1.0 ? $"{(amps * 1000):F2} mA" : $"{amps:F2} A";
                    liveStatsLabel.text =
                        $"Voltage Drop: {voltageDrop:F2} V\nCurrent: {currentText}";
                }
            }
        }
        else
        {
            if (currentlySelectedVE != null)
                liveStatsLabel.text = "Simulation Paused";
        }
    }
}

public class PlacedComponentData
{
    public string instanceId = System.Guid.NewGuid().ToString();
    public FritzingComponentAsset asset;
    public double componentValue;
}
