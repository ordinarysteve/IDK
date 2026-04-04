using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class CodeDrivenUI : MonoBehaviour
{
    [Header("Drag your transparent PNG icons here!")]
    public Texture2D resistorIcon;
    public Texture2D diodeIcon; // Example of how easy it is to add more

    private VisualElement root;

    void OnEnable()
    {
        // Grab the UI Document root
        root = GetComponent<UIDocument>().rootVisualElement;

        // Build the main left-hand Toolbox Panel
        VisualElement toolboxPanel = new VisualElement();
        toolboxPanel.style.width = 100;
        toolboxPanel.style.height = Length.Percent(100);
        toolboxPanel.style.backgroundColor = new Color(0.12f, 0.12f, 0.12f, 1f); // Sleek dark gray
        toolboxPanel.style.paddingTop = 20;
        toolboxPanel.style.alignItems = Align.Center; // Centers buttons horizontally
        toolboxPanel.style.position = Position.Absolute;
        toolboxPanel.style.left = 0;

        // Add buttons automatically (Flexbox will stack them perfectly!)
        if (resistorIcon != null)
            toolboxPanel.Add(CreateToolButton(resistorIcon, "Resistor"));

        if (diodeIcon != null)
            toolboxPanel.Add(CreateToolButton(diodeIcon, "Diode"));

        // Attach panel to the screen
        root.Add(toolboxPanel);
    }

    // --- HELPER FUNCTION: Makes adding new tools effortless ---
    private VisualElement CreateToolButton(Texture2D icon, string toolName)
    {
        VisualElement btn = new VisualElement();

        // Base Styling
        btn.style.width = 64;
        btn.style.height = 64;
        btn.style.marginBottom = 15; // Spaces them out automatically
        btn.style.backgroundColor = new Color(0.25f, 0.25f, 0.25f, 1f);
        btn.style.backgroundImage = new StyleBackground(icon);

        // Rounded corners
        btn.style.borderTopLeftRadius = 10;
        btn.style.borderTopRightRadius = 10;
        btn.style.borderBottomLeftRadius = 10;
        btn.style.borderBottomRightRadius = 10;

        // Hover Effect (Lighten color)
        btn.RegisterCallback<MouseEnterEvent>(e =>
        {
            btn.style.backgroundColor = new Color(0.4f, 0.4f, 0.4f, 1f);
        });

        // Un-Hover Effect (Revert color)
        btn.RegisterCallback<MouseLeaveEvent>(e =>
        {
            btn.style.backgroundColor = new Color(0.25f, 0.25f, 0.25f, 1f);
        });

        // Click Logic
        btn.RegisterCallback<PointerDownEvent>(e =>
        {
            Debug.Log($"Selected {toolName} from the Toolbox!");
            // TODO: Tell WireManager to spawn the ghost object here
        });

        return btn;
    }
}
