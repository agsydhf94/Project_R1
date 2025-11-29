
namespace R1
{
    public static class ViewModelBinder
    {
        public static void Bind<T>(UIBase<T> view, T viewModel) where T : class
        {
            view.Init(viewModel);
            view.Open();
        }
    }
}
