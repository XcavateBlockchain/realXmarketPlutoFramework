using PlutoFramework.Model.Messaging;

namespace PlutoFramework.Components.Messaging;

public partial class MessageInputBarView : ContentView
{
    public static readonly BindableProperty MessagingModelProperty =
        BindableProperty.Create(nameof(MessagingModel), typeof(MessagingModel), typeof(MessageInputBarView));

    public static readonly BindableProperty NamespaceIdProperty =
        BindableProperty.Create(nameof(NamespaceId), typeof(int), typeof(MessageInputBarView));

    public static readonly BindableProperty BucketIdProperty =
        BindableProperty.Create(nameof(BucketId), typeof(int), typeof(MessageInputBarView));

    public static readonly BindableProperty BucketEncryptionKeyProperty =
        BindableProperty.Create(nameof(BucketEncryptionKey), typeof(byte[]), typeof(MessageInputBarView));

    public MessagingModel MessagingModel
    {
        get => (MessagingModel)GetValue(MessagingModelProperty);
        set => SetValue(MessagingModelProperty, value);
    }

    public int NamespaceId
    {
        get => (int)GetValue(NamespaceIdProperty);
        set => SetValue(NamespaceIdProperty, value);
    }

    public int BucketId
    {
        get => (int)GetValue(BucketIdProperty);
        set => SetValue(BucketIdProperty, value);
    }

    public byte[] BucketEncryptionKey
    {
        get => (byte[])GetValue(BucketEncryptionKeyProperty);
        set => SetValue(BucketEncryptionKeyProperty, value);
    }

    public MessageInputBarView()
    {
        InitializeComponent();
    }

    protected override void OnPropertyChanged(string propertyName = null)
    {
        base.OnPropertyChanged(propertyName);

        if (propertyName == MessagingModelProperty.PropertyName ||
            propertyName == NamespaceIdProperty.PropertyName ||
            propertyName == BucketIdProperty.PropertyName ||
            propertyName == BucketEncryptionKeyProperty.PropertyName)
        {
            if (MessagingModel != null && BucketEncryptionKey != null)
            {
                BindingContext = new MessageInputBarViewModel(MessagingModel, NamespaceId, BucketId, BucketEncryptionKey);
            }
        }
    }
}
