using System;
using System.Collections.Generic;
using System.Drawing;
using MosaicCensorSystem.Detection;

namespace MosaicCensorSystem.UI
{
    public interface IGuiController : IDisposable
    {
        // --- Events ---
        event Action<bool> DetectionToggled;
        event Action<bool> CensoringToggled;
        event Action<bool> StickerToggled;
        event Action<bool> CaptionToggled;
        event Action<CensorType> CensorTypeChanged;
        event Action<int> StrengthChanged;
        event Action<float> ConfidenceChanged;
        
        event Action StartClicked;
        event Action StopClicked;
        event Action CaptureAndSaveClicked;
        event Action<List<string>> TargetsChanged;
        event Action GpuSetupClicked;
        event Action<bool> DpiCompatToggled;
        event Action<bool> ModelTypeChanged;

        // Custom event for new Sticker Setup (not in GuiController, but needed for MockupUIForm)
        event Action<string, bool, List<string>> TargetStickerConfigChanged;

        // --- Methods ---
        void UpdateStatus(string message, Color color);
        void LogMessage(string message);
        void SetRunningState(bool isRunning);
        void UpdateGpuStatus(string status);
        void SetObbModelAvailable(bool available);
        void RebuildTargetCheckboxes(string[] availableTargets);
    }
}
