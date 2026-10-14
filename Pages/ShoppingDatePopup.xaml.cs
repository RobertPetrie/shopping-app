namespace shopping_app.Pages
{
    /// <summary>
    /// Shared date popup for new lists and existing lists.
    /// Nothing is saved until the calling page receives a confirmed date.
    /// </summary>
    public partial class ShoppingDatePopup : ContentView
    {
        private TaskCompletionSource<DateOnly?>? selectionSource;

        public ShoppingDatePopup()
        {
            InitializeComponent();
        }

        public bool IsOpen => selectionSource is not null;

        public Task<DateOnly?> ShowAsync(DateOnly initialDate)
        {
            if (IsOpen)
            {
                throw new InvalidOperationException("The date picker is already open.");
            }

            selectionSource = new TaskCompletionSource<DateOnly?>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            // New lists supply today; existing lists supply their saved date.
            ShoppingDatePicker.Date = initialDate.ToDateTime(TimeOnly.MinValue);
            IsVisible = true;
            return selectionSource.Task;
        }

        private void OnConfirmClicked(object? sender, EventArgs e)
        {
            // The chosen date has no time or timezone component.
            Complete(DateOnly.FromDateTime((DateTime)ShoppingDatePicker.Date));
        }

        private void OnCancelClicked(object? sender, EventArgs e)
        {
            Cancel();
        }

        public void Cancel()
        {
            Complete(null);
        }

        private void Complete(DateOnly? chosenDate)
        {
            var source = selectionSource;
            if (source is null)
            {
                return;
            }

            selectionSource = null;
            IsVisible = false;
            source.TrySetResult(chosenDate);
        }
    }
}
