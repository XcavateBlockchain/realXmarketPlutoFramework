using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;
using PlutoFramework.Model.Xcavate;

namespace PlutoFramework.Components.Xcavate;

public partial class XcavateRoleBadgeView : ContentView
{
    private readonly Border border;
    private readonly Label roleLabel;

    public static readonly BindableProperty RoleProperty = BindableProperty.Create(
        nameof(Role), typeof(XcavateRole), typeof(XcavateRoleBadgeView),
        defaultBindingMode: BindingMode.TwoWay,
        propertyChanging: (bindable, oldValue, newValue) =>
        {
            var control = (XcavateRoleBadgeView)bindable;
            var role = (XcavateRole)newValue;

            control.roleLabel.Text = role switch
            {
                XcavateRole.RegionalOperator => "Regional operator",
                XcavateRole.RealEstateInvestor => "Real estate investor",
                XcavateRole.RealEstateDeveloper => "Real estate developer",
                XcavateRole.LettingAgent => "Letting agent",
                XcavateRole.SpvConfirmation => "SPV confirmation",
                XcavateRole.ModuleCreator => "Module creator",
                XcavateRole.ModuleSponsor => "Module sponsor",
                XcavateRole.ModuleBooker => "Module booker",
                XcavateRole.ModuleDeliverer => "Module deliverer",
                XcavateRole.ModuleAIAgent => "Module AI agent",
                XcavateRole.ModuleRecipient => "Module recipient",
                _ => role.ToString(),
            };

            switch (role)
            {
                case XcavateRole.RealEstateDeveloper:
                    control.border.BackgroundColor = Color.FromArgb("#1A457461");
                    control.roleLabel.TextColor = Color.FromArgb("#457461");
                    break;
                case XcavateRole.RealEstateInvestor:
                    control.border.BackgroundColor = Color.FromArgb("#1ADC7DA6");
                    control.roleLabel.TextColor = Color.FromArgb("#DC7DA6");
                    break;
                case XcavateRole.LettingAgent:
                    control.border.BackgroundColor = Color.FromArgb("#1A9678AE");
                    control.roleLabel.TextColor = Color.FromArgb("#9678AE");
                    break;
                case XcavateRole.Lawyer:
                    control.border.BackgroundColor = Color.FromArgb("#1A4E7DDC");
                    control.roleLabel.TextColor = Color.FromArgb("#4E7DDC");
                    break;
                default:
                    control.border.BackgroundColor = Color.FromArgb("#1A888888");
                    control.roleLabel.TextColor = Color.FromArgb("#888888");
                    break;
            }
        });

    public XcavateRoleBadgeView()
    {
        roleLabel = new Label
        {
            Margin = new Thickness(12, 5),
            Text = "None",
            TextColor = Color.FromArgb("#888888"),
            VerticalTextAlignment = TextAlignment.Center,
            HorizontalTextAlignment = TextAlignment.Center,
            FontSize = 10,
        };

        AbsoluteLayout.SetLayoutBounds(roleLabel, new Rect(0.5, 0.5, AbsoluteLayout.AutoSize, AbsoluteLayout.AutoSize));
        AbsoluteLayout.SetLayoutFlags(roleLabel, AbsoluteLayoutFlags.PositionProportional);

        var layout = new AbsoluteLayout
        {
            Children = { roleLabel },
        };

        border = new Border
        {
            Padding = 0,
            HeightRequest = 24,
            StrokeThickness = 0,
            BackgroundColor = Color.FromArgb("#1A888888"),
            StrokeShape = new RoundRectangle
            {
                CornerRadius = new CornerRadius(12),
            },
            Content = layout,
        };

        Content = border;
    }

    public XcavateRole Role
    {
        get => (XcavateRole)GetValue(RoleProperty);
        set => SetValue(RoleProperty, value);
    }
}
