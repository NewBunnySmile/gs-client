namespace GagSpeak.Gui.Components;
public interface IClipboardHandler<T>
{
    void Copy(T item);
    void Import(Action<T> onAddAction);
}
