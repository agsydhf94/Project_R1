using UnityEngine;

namespace R1
{
    public abstract class UIBase<T> : MonoBehaviour where T : class
    {
        protected T viewModel;

        public virtual void Init(T vm)
        {
            viewModel = vm;
        }

        public virtual void Open() => gameObject.SetActive(true);
        public virtual void Close() => gameObject.SetActive(false);
    }
}
