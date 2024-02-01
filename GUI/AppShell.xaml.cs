namespace GUI {
    public partial class AppShell : Shell {
        public AppShell() {
            InitializeComponent();

            Routing.RegisterRoute(nameof(Views.CleanPage), typeof(Views.CleanPage));
            Routing.RegisterRoute(nameof(Views.SplitPage), typeof(Views.SplitPage));
            Routing.RegisterRoute(nameof(Views.MergePage), typeof(Views.MergePage));
        }
    }
}
