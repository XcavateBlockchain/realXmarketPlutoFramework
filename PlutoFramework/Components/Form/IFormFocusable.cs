namespace PlutoFramework.Components.Form;

/// <summary>
/// A form field that wraps its own <see cref="Entry"/>.
/// </summary>
/// <remarks>
/// Moving focus along a form has to land on the inner entry: the wrapping
/// <see cref="ContentView"/> accepts focus but has no keyboard to raise, so focusing it would
/// dismiss the keyboard instead of carrying it to the next field.
/// </remarks>
public interface IFormFocusable
{
    void FocusEntry();
}
