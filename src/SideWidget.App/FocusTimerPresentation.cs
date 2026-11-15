using SideWidget.Core;

namespace SideWidget.App;

internal sealed record FocusTimerPresentation(string Caption, string Value, string Action, string Status, double Progress)
{
    public bool Ringing => Status == "ringing";
    public bool NeedsFullWidth => Status != "running";
    public string AccessibleName => Ringing
        ? DesktopLanguage.Text("Время вышло. Нажмите, чтобы остановить сигнал")
        : DesktopLanguage.Text("{0}, осталось {1}", Caption, Value);

    public string CompactLabel(bool clockOnly) => Ringing
        ? $"{Value}   {Action}"
        : clockOnly && !NeedsFullWidth ? Value : $"{Caption}   {Value}";

    public static FocusTimerPresentation? Create(FocusTimerState state, int remaining)
    {
        if (state.Status == "idle") return null;
        var caption = state.Status switch
        {
            "paused" => "Пауза",
            "ready" => "Готов к старту",
            _ => state.Mode switch
            {
                "alarm" => "Будильник",
                "timer" => "Таймер",
                _ => state.Phase == "work" ? "Работа" : "Перерыв"
            }
        };
        var ringing = state.Status == "ringing";
        var value = ringing ? DesktopLanguage.Text("Время вышло") : FocusTimer.Format(remaining);
        var action = ringing ? DesktopLanguage.Text("Нажмите") : "";
        var progress = state.Status is "running" or "paused" && state.DurationSeconds > 0
            ? Math.Clamp(1 - (double)remaining / state.DurationSeconds, 0, 1) : 0;
        return new(DesktopLanguage.Text(caption), value, action, state.Status, progress);
    }
}
