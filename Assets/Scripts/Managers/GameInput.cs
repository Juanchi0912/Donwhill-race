using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.DualShock;
#endif

/// <summary>
/// Punto único de lectura de controles (teclado y mando).
/// Centralizarlo aquí permite cambiar el mapeo sin tocar la bici ni los menús.
/// Las propiedades "Pressed" solo son válidas dentro de Update (son del frame actual).
/// </summary>
public static class GameInput
{
    // Zona muerta de los gatillos: algunos mandos no vuelven exactamente a 0.
    private const float TriggerDeadzone = 0.1f;

#if ENABLE_INPUT_SYSTEM
    // Último dispositivo usado (para mostrar teclas o botones del mando en pantalla).
    private static bool usingGamepad;
    private static bool deviceKnown;
    private static int deviceCheckFrame = -1;

    // Sin recarga de dominio los estáticos sobreviven entre Play y Play: se reinician aquí.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        usingGamepad = false;
        deviceKnown = false;
        deviceCheckFrame = -1;
    }
#endif

    /// <summary>
    /// Pedalear, de 0 a 1. W / ↑ / botón sur (A de Xbox, Cruz de PlayStation) / Gatillo derecho.
    /// El botón sur pedalea al máximo; el gatillo permite dosificar.
    /// </summary>
    public static float Throttle
    {
        get
        {
            float value = 0f;
#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            if (kb != null && (kb.wKey.isPressed || kb.upArrowKey.isPressed)) value = 1f;

            Gamepad pad = Gamepad.current;
            if (pad != null)
            {
                value = Mathf.Max(value, ApplyTriggerDeadzone(pad.rightTrigger.ReadValue()));
                if (pad.buttonSouth.isPressed) value = 1f;
            }
#else
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) value = 1f;
#endif
            return value;
        }
    }

    /// <summary>Frenar, de 0 a 1. S / ↓ / botón oeste (X de Xbox, Cuadrado de PlayStation) / Gatillo izquierdo.</summary>
    public static float Brake
    {
        get
        {
            float value = 0f;
#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            if (kb != null && (kb.sKey.isPressed || kb.downArrowKey.isPressed)) value = 1f;

            Gamepad pad = Gamepad.current;
            if (pad != null)
            {
                value = Mathf.Max(value, ApplyTriggerDeadzone(pad.leftTrigger.ReadValue()));
                if (pad.buttonWest.isPressed) value = 1f;
            }
#else
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) value = 1f;
#endif
            return value;
        }
    }

    /// <summary>Girar, de -1 (izquierda) a 1 (derecha). A D / ← → / Stick izquierdo / Cruceta.</summary>
    public static float Steer
    {
        get
        {
            float value = 0f;
#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) value -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) value += 1f;
            }

            Gamepad pad = Gamepad.current;
            if (pad != null)
            {
                // El stick ya trae zona muerta por defecto en el Input System.
                value += pad.leftStick.x.ReadValue();
                value += pad.dpad.x.ReadValue();
            }
#else
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) value -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) value += 1f;
#endif
            // Se limita por si teclado y mando se usan a la vez.
            return Mathf.Clamp(value, -1f, 1f);
        }
    }

    /// <summary>Agacharse (mantener). Shift izquierdo / LB (L1 de PlayStation).</summary>
    public static bool Crouch
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            Gamepad pad = Gamepad.current;
            return (kb != null && kb.leftShiftKey.isPressed)
                || (pad != null && pad.leftShoulder.isPressed);
#else
            return Input.GetKey(KeyCode.LeftShift);
#endif
        }
    }

    /// <summary>Saltar (este frame). Espacio / RB (R1 de PlayStation): se salta sin soltar el pedaleo.</summary>
    public static bool JumpPressed
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            Gamepad pad = Gamepad.current;
            return (kb != null && kb.spaceKey.wasPressedThisFrame)
                || (pad != null && pad.rightShoulder.wasPressedThisFrame);
#else
            return Input.GetKeyDown(KeyCode.Space);
#endif
        }
    }

    /// <summary>Reaparecer en el último checkpoint (este frame). R / botón norte (Y de Xbox, Triángulo de PlayStation).</summary>
    public static bool RespawnPressed
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            Gamepad pad = Gamepad.current;
            return (kb != null && kb.rKey.wasPressedThisFrame)
                || (pad != null && pad.buttonNorth.wasPressedThisFrame);
#else
            return Input.GetKeyDown(KeyCode.R);
#endif
        }
    }

    /// <summary>Empezar / confirmar (este frame). Enter / Start / botón sur (A de Xbox, Cruz de PlayStation).</summary>
    public static bool ConfirmPressed
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            Gamepad pad = Gamepad.current;
            return (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
                || (pad != null && (pad.startButton.wasPressedThisFrame || pad.buttonSouth.wasPressedThisFrame));
#else
            return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
#endif
        }
    }

    /// <summary>Volver en menús y en la meta (este frame). Esc / botón este (B de Xbox, Círculo de PlayStation) / Select.</summary>
    public static bool BackPressed
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            Gamepad pad = Gamepad.current;
            return (kb != null && kb.escapeKey.wasPressedThisFrame)
                || (pad != null && (pad.buttonEast.wasPressedThisFrame || pad.selectButton.wasPressedThisFrame));
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }
    }

    /// <summary>
    /// Reiniciar la bajada en plena carrera (este frame). Esc / Select.
    /// No usa B: es fácil apretarlo sin querer y se perdería la bajada.
    /// </summary>
    public static bool RestartPressed
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            Gamepad pad = Gamepad.current;
            return (kb != null && kb.escapeKey.wasPressedThisFrame)
                || (pad != null && pad.selectButton.wasPressedThisFrame);
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }
    }

    /// <summary>True si lo último que se tocó fue un mando (se usa para mostrar sus botones en pantalla).</summary>
    public static bool UsingGamepad
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            if (deviceCheckFrame == Time.frameCount) return usingGamepad;
            deviceCheckFrame = Time.frameCount;
            Gamepad pad = Gamepad.current;
            if (!deviceKnown)
            {
                // Al empezar: si hay un mando conectado se asume que se juega con él.
                usingGamepad = pad != null;
                deviceKnown = true;
            }
            if (pad == null) usingGamepad = false;
            else if (AnyGamepadPress(pad)) usingGamepad = true;
            else if (AnyKeyboardOrMouse()) usingGamepad = false;
            return usingGamepad;
#else
            return false;
#endif
        }
    }

    /// <summary>True si el mando conectado es de PlayStation (los botones se nombran Cruz, Círculo...).</summary>
    public static bool IsPlayStationPad
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            return Gamepad.current is DualShockGamepad;
#else
            return false;
#endif
        }
    }

    /// <summary>
    /// Cambia las marcas {A} {B} {X} {Y} {START} {SELECT} {RT} {LT} {RB} {LB} por el nombre del botón
    /// según el mando conectado (Xbox y genéricos, o PlayStation).
    /// </summary>
    public static string PadText(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        bool ps = IsPlayStationPad;
        return text
            .Replace("{A}", ps ? "Cruz" : "A")
            .Replace("{B}", ps ? "Círculo" : "B")
            .Replace("{X}", ps ? "Cuadrado" : "X")
            .Replace("{Y}", ps ? "Triángulo" : "Y")
            .Replace("{START}", ps ? "Options" : "Start")
            .Replace("{SELECT}", ps ? "Share" : "Select")
            .Replace("{RT}", ps ? "R2" : "RT")
            .Replace("{LT}", ps ? "L2" : "LT")
            .Replace("{RB}", ps ? "R1" : "RB")
            .Replace("{LB}", ps ? "L1" : "LB");
    }

    /// <summary>Clic izquierdo del mouse (este frame). Sirve para saltar la presentación.</summary>
    public static bool ClickPressed
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            return mouse != null && mouse.leftButton.wasPressedThisFrame;
#else
            return Input.GetMouseButtonDown(0);
#endif
        }
    }

    /// <summary>Cualquier botón de "aceptar" o "volver" (este frame): para pantallas que se saltan.</summary>
    public static bool AnySkipPressed => ConfirmPressed || BackPressed || JumpPressed || ClickPressed;

    private static float ApplyTriggerDeadzone(float value)
    {
        return value < TriggerDeadzone ? 0f : value;
    }

#if ENABLE_INPUT_SYSTEM
    private static bool AnyGamepadPress(Gamepad pad)
    {
        // Los botones, gatillos y direcciones del stick son ButtonControl: basta con ver si alguno se apretó.
        foreach (InputControl control in pad.allControls)
        {
            if (control is ButtonControl button && button.wasPressedThisFrame) return true;
        }
        return false;
    }

    private static bool AnyKeyboardOrMouse()
    {
        Keyboard kb = Keyboard.current;
        if (kb != null && kb.anyKey.wasPressedThisFrame) return true;
        Mouse mouse = Mouse.current;
        if (mouse == null) return false;
        // Un movimiento claro del mouse (no un temblor) también cuenta.
        return mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame
            || mouse.delta.ReadValue().sqrMagnitude > 64f;
    }
#endif
}
