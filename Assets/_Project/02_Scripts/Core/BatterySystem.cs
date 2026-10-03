using System;
using UnityEngine;

namespace LastLight.Core
{
    /// <summary>
    /// Battery level management.
    /// For values like the field of view and HUD gauge, you can read the normalized values directly at each frame (by polling).
    /// only for effects that require 'instantaneous changes' (such as DOTween pulses), like obtaining a battery pack
    /// Reduce unnecessary delegate calls.
    /// </summary>
    public class BatterySystem : MonoBehaviour
    {
        [SerializeField] private float maxBattery = 100f;
        [SerializeField] private float drainPerSecond = 1.2f;

        private float _current;
        private bool _depletedFired;

        /// Exists in only one <summary> scene. The battery pack and other components can be located and used without a reference connection.</summary>
        public static BatterySystem Instance { get; private set; }

        public float Max => maxBattery;
        public float Current => _current;
        public float Normalized => _current / maxBattery;
        public bool IsEmpty => _current <= 0f;

        /// <summary>Normal (automatic) consumption rate. Refer to this value when calculating additional consumption, such as for flashlight disposal..</summary>
        public float DrainPerSecond => drainPerSecond;

        /// <summary>Occurs when the battery pack is acquired and charged immediately.</summary>
        public event Action OnPickup;

        /// <summary>Occurs only once when the battery reaches 0.</summary>
        public event Action OnDepleted;

        private bool _isOn = true;

        /// <summary>Is the flashlight on. When it turns off, the battery consumption stops completely and the visibility is lost.</summary>
        public bool IsOn => _isOn;

        /// <summary>Used when the state changes (e.g., for UI icon updates).</summary>
        public event Action<bool> OnToggled;



        private void Awake()
        {
            Instance = this;
            _current = maxBattery;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            if (!_isOn) return; // If it's off, the consumption stops

            if (_current <= 0f) return;

            _current = Mathf.Max(0f, _current - drainPerSecond * Time.deltaTime);

            if (_current <= 0f && !_depletedFired)
            {
                _depletedFired = true;
                OnDepleted?.Invoke();
            }
        }

        /// <summary>Called when a battery pack is obtained. Recharge by the amount (cannot exceed the maximum).</summary>
        public void Add(float amount)
        {
            if (amount <= 0f) return;

            _current = Mathf.Min(maxBattery, _current + amount);
            _depletedFired = false;
            OnPickup?.Invoke();
        }

        /// <summary>Charged up to a ratio of (0–1) relative to the maximum battery. Even if the maximum battery pack changes, it always fills with the same ratio.</summary>
        public void AddPercent(float percent) => Add(maxBattery * percent);

        /// <summary>Flashlight ON/OFF. You can call it directly from the UI button (link this method to OnClick).</summary>
        public void Toggle() => SetOn(!_isOn);

        public void SetOn(bool on)
        {
            if (_isOn == on) return;
            _isOn = on;
            OnToggled?.Invoke(on);
        }

        /// <summary>Consumes additional battery (such as extra power during flashlight deployment) in addition to automatic consumption. amount is the absolute value
        public void Drain(float amount)
        {
            if (amount <= 0f || _current <= 0f) return;

            _current = Mathf.Max(0f, _current - amount);

            if (_current <= 0f && !_depletedFired)
            {
                _depletedFired = true;
                OnDepleted?.Invoke();
            }
        }
    }
}