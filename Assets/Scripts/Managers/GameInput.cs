using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
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

    /// <summary>Pedalear, de 0 a 1. W / ↑ / Gatillo derecho.</summary>
    public static float Throttle
    {
        get
        {
            float value = 0f;
#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            if (kb != null && (kb.wKey.isPressed || kb.upArrowKey.isPressed)) value = 1f;

            Gamepad pad = Gamepad.current;
            if (pad != null) value = Mathf.Max(value, ApplyTriggerDeadzone(pad.rightTrigger.ReadValue()));
#else
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) value = 1f;
#endif
            return value;
        }
    }

    /// <summary>Frenar, de 0 a 1. S / ↓ / Gatillo izquierdo.</summary>
    public static float Brake
    {
        get
        {
            float value = 0f;
#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            if (kb != null && (kb.sKey.isPressed || kb.downArrowKey.isPressed)) value = 1f;

            Gamepad pad = Gamepad.current;
            if (pad != null) value = Mathf.Max(value, ApplyTriggerDeadzone(pad.leftTrigger.ReadValue()));
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

    /// <summary>Agacharse (mantener). Shift izquierdo / botón oeste (X de Xbox, Cuadrado de PlayStation).</summary>
    public static bool Crouch
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            Gamepad pad = Gamepad.current;
            return (kb != null && kb.leftShiftKey.isPressed)
                || (pad != null && pad.buttonWest.isPressed);
#else
            return Input.GetKey(KeyCode.LeftShift);
#endif
        }
    }

    /// <summary>Saltar (este frame). Espacio / botón sur (A de Xbox, Cruz de PlayStation).</summary>
    public static bool JumpPressed
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            Gamepad pad = Gamepad.current;
            return (kb != null && kb.spaceKey.wasPressedThisFrame)
                || (pad != null && pad.buttonSouth.wasPressedThisFrame);
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

    /// <summary>Empezar / confirmar (este frame). Enter / Start.</summary>
    public static bool ConfirmPressed
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            Gamepad pad = Gamepad.current;
            return (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
                || (pad != null && pad.startButton.wasPressedThisFrame);
#else
            return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
#endif
        }
    }

    /// <summary>Volver / reiniciar (este frame). Esc / Select.</summary>
    public static bool BackPressed
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
}
