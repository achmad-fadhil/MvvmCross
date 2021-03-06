﻿using MvvmCross.Core.ViewModels;

namespace MasterDetailExample.Core.ViewModels
{
    public class Option1ViewModel : MvxViewModel
    {
        private string _message;
        public string Message { get { return _message; } set { SetProperty(ref _message, value); } }

        public Option1ViewModel()
        {
            Message = "Opción 1";
        }
    }
}
