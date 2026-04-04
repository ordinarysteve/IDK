using System;
using UnityEngine;

public class ResistorColorManager : MonoBehaviour
{
    // Drag your 4 white band SpriteRenderers into these slots in the Inspector!
    public SpriteRenderer band1Renderer;
    public SpriteRenderer band2Renderer;
    public SpriteRenderer multiplierRenderer;
    public SpriteRenderer toleranceRenderer;

    // The standard electronic color code (0 to 9)
    private readonly Color[] resistorColors = new Color[]
    {
        Color.black, // 0
        new Color(0.6f, 0.3f, 0f), // 1: Brown
        Color.red, // 2
        new Color(1f, 0.5f, 0f), // 3: Orange
        Color.yellow, // 4
        Color.green, // 5
        Color.blue, // 6
        new Color(0.5f, 0f, 0.5f), // 7: Violet
        Color.gray, // 8
        Color.white, // 9
    };

    private readonly Color goldColor = new Color(0.85f, 0.65f, 0.13f);

    // Call this whenever the user changes the resistor value!
    public void UpdateColorBands(double ohms)
    {
        // Convert to an integer to avoid floating point weirdness
        long value = (long)Math.Round(ohms);

        if (value < 10)
        {
            Debug.LogWarning(
                "Values under 10 ohms require silver/gold multipliers, keeping it simple for now!"
            );
            return;
        }

        // The easiest way to parse the digits is to turn the number into a string
        string valStr = value.ToString();

        // 1. Get the first two digits
        int digit1 = int.Parse(valStr[0].ToString());
        int digit2 = int.Parse(valStr[1].ToString());

        // 2. The multiplier is simply however many digits are left over
        // E.g., for 4700, length is 4. We used 2 digits. 4 - 2 = 2 zeros.
        int multiplier = valStr.Length - 2;

        // Prevent crashes if they enter something absurdly huge like 1000 GigaOhms
        multiplier = Mathf.Clamp(multiplier, 0, 9);

        // 3. Apply the colors!
        band1Renderer.color = resistorColors[digit1];
        band2Renderer.color = resistorColors[digit2];
        multiplierRenderer.color = resistorColors[multiplier];

        // Default to Gold (5% tolerance) for now
        toleranceRenderer.color = goldColor;
    }
}
