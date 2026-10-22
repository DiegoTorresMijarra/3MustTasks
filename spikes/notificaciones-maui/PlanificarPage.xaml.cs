namespace ThreeMustTasks.Spikes.Notifications;

public partial class PlanificarPage : ContentPage
{
    public PlanificarPage() => InitializeComponent();

    private async void OnBack(object? sender, EventArgs e) => await Shell.Current.GoToAsync("//home");
}
