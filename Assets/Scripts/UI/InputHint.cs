using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Texto de ayuda que muestra las teclas o los botones del mando según lo último que se usó.
/// En el texto del mando, {A} {B} {X} {Y} {RB} {LB} {RT} {LT} {START} {SELECT} se cambian por el
/// nombre del botón del mando conectado (Xbox o PlayStation).
/// </summary>
public class InputHint : MonoBehaviour
{
    [SerializeField] private Text label = null;

    [Tooltip("Texto cuando se juega con teclado y mouse.")]
    [SerializeField, TextArea(2, 6)] private string keyboardText = string.Empty;

    [Tooltip("Texto cuando se juega con mando. Usa {A}, {B}, {X}, {Y}, {RB}, {LB}, {RT}, {LT}, {START}, {SELECT}.")]
    [SerializeField, TextArea(2, 6)] private string gamepadText = string.Empty;

    // 0 = teclado, 1 = mando Xbox/genérico, 2 = mando PlayStation. Solo se reescribe el texto al cambiar.
    private int shownStyle = -1;

    /// <summary>Lo usa el constructor de escenas.</summary>
    public void Configure(Text target, string keyboard, string gamepad)
    {
        label = target;
        keyboardText = keyboard;
        gamepadText = gamepad;
    }

    private void Awake()
    {
        if (label == null) label = GetComponent<Text>();
    }

    private void OnEnable()
    {
        shownStyle = -1;
        Refresh();
    }

    private void Update()
    {
        Refresh();
    }

    private void Refresh()
    {
        if (label == null) return;
        int style = GameInput.UsingGamepad ? (GameInput.IsPlayStationPad ? 2 : 1) : 0;
        if (style == shownStyle) return;
        shownStyle = style;
        label.text = style == 0 ? keyboardText : GameInput.PadText(gamepadText);
    }
}
