﻿// MvxTabsFragmentActivity.cs
// (c) Copyright Cirrious Ltd. http://www.cirrious.com
// MvvmCross is licensed using Microsoft Public License (Ms-PL)
// Contributions and inspirations noted in readme.md and license.txt
//
// Project Lead - Stuart Lodge, @slodge, me@slodge.com

using System;
using System.Collections.Generic;
using System.Linq;
using Android;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Support.V4.App;
using Android.Views;
using Android.Widget;
using Java.Lang;
using MvvmCross.Binding.Droid.BindingContext;
using MvvmCross.Core.ViewModels;
using MvvmCross.Platform.Core;
using Object = Java.Lang.Object;

namespace MvvmCross.Droid.Support.V4
{
    [Register("mvvmcross.droid.support.v4.MvxTabsFragmentActivity")]
    public abstract class MvxTabsFragmentActivity
        : MvxFragmentActivity, TabHost.IOnTabChangeListener, ViewTreeObserver.IOnGlobalLayoutListener
    {

        private const string SavedTabIndexStateKey = "__savedTabIndex";

        private readonly Dictionary<string, TabInfo> _lookup = new Dictionary<string, TabInfo>();

        private readonly int _layoutId;
        private TabHost _tabHost;
        private TabInfo _currentTab;
        private readonly int _tabContentId;

        protected MvxTabsFragmentActivity(int layoutId, int tabContentId)
        {
            _layoutId = layoutId;
            _tabContentId = tabContentId;
        }

        protected MvxTabsFragmentActivity(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
        }

        protected class TabInfo
        {
            public string Tag { get; private set; }
            public Type FragmentType { get; private set; }
            public Bundle Bundle { get; private set; }
            public IMvxViewModel ViewModel { get; private set; }

            public Fragment CachedFragment { get; set; }

            public TabInfo(string tag, Type fragmentType, Bundle bundle, IMvxViewModel viewModel)
            {
                Tag = tag;
                FragmentType = fragmentType;
                Bundle = bundle;
                ViewModel = viewModel;
            }
        }

        private class TabFactory
            : Object, TabHost.ITabContentFactory
        {
            private readonly Context _context;

            public TabFactory(Context context)
            {
                _context = context;
            }

            public View CreateTabContent(string tag)
            {
                var v = new View(_context);
                v.SetMinimumWidth(0);
                v.SetMinimumHeight(0);
                return v;
            }
        }

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            SetContentView(_layoutId);

            _view = Window.DecorView.RootView;

            _view.ViewTreeObserver.AddOnGlobalLayoutListener(this);

            InitializeTabHost(savedInstanceState);

            if (savedInstanceState != null)
            {
                _tabHost.SetCurrentTabByTag(savedInstanceState.GetString(SavedTabIndexStateKey));
            }
        }

        public override void SetContentView(int layoutResId)
        {
            var view = this.BindingInflate(layoutResId, null);

            SetContentView(view);
        }

        protected override void OnSaveInstanceState(Bundle outState)
        {
            outState.PutString(SavedTabIndexStateKey, _tabHost.CurrentTabTag);
            base.OnSaveInstanceState(outState);
        }

        private void InitializeTabHost(Bundle args)
        {
            _tabHost = (TabHost)FindViewById(Resource.Id.TabHost);
            _tabHost.Setup();

            AddTabs(args);

            if (_lookup.Any())
                OnTabChanged(_lookup.First().Key);

            _tabHost.SetOnTabChangedListener(this);
        }

        protected abstract void AddTabs(Bundle args);

        protected void AddTab<TFragment>(string tagAndSpecName, string tabName, Bundle args,
                                         IMvxViewModel viewModel)
        {
            var tabSpec = _tabHost.NewTabSpec(tagAndSpecName).SetIndicator(tabName);
            AddTab<TFragment>(args, viewModel, tabSpec);
        }

        protected void AddTab<TFragment>(Bundle args, IMvxViewModel viewModel, TabHost.TabSpec tabSpec)
        {
            var tabInfo = new TabInfo(tabSpec.Tag, typeof(TFragment), args, viewModel);
            AddTab(this, _tabHost, tabSpec, tabInfo);
            _lookup.Add(tabInfo.Tag, tabInfo);
        }

        private static void AddTab(MvxTabsFragmentActivity activity,
            TabHost tabHost,
            TabHost.TabSpec tabSpec,
            TabInfo tabInfo)
        {
            // Attach a Tab view factory to the spec
            tabSpec.SetContent(new TabFactory(activity));
            string tag = tabSpec.Tag;

            // Check to see if we already have a CachedFragment for this tab, probably
            // from a previously saved state.  If so, deactivate it, because our
            // initial state is that a tab isn't shown.
            tabInfo.CachedFragment = activity.SupportFragmentManager.FindFragmentByTag(tag);
            if (tabInfo.CachedFragment != null && !tabInfo.CachedFragment.IsDetached)
            {
                var ft = activity.SupportFragmentManager.BeginTransaction();
                ft.Detach(tabInfo.CachedFragment);
                ft.Commit();
                activity.SupportFragmentManager.ExecutePendingTransactions();
            }

            tabHost.AddTab(tabSpec);
        }

        public virtual void OnTabChanged(string tag)
        {
            var newTab = _lookup[tag];
            if (_currentTab != newTab)
            {
                var ft = SupportFragmentManager.BeginTransaction();
                OnTabFragmentChanging(tag, ft);
                if (_currentTab?.CachedFragment != null)
                {
                    ft.Detach(_currentTab.CachedFragment);
                }
                if (newTab != null)
                {
                    if (newTab.CachedFragment == null)
                    {
                        newTab.CachedFragment = Fragment.Instantiate(this,
                                                                     FragmentJavaName(newTab.FragmentType),
                                                                     newTab.Bundle);
                        FixupDataContext(newTab);
                        ft.Add(_tabContentId, newTab.CachedFragment, newTab.Tag);
                    }
                    else
                    {
                        FixupDataContext(newTab);
                        ft.Attach(newTab.CachedFragment);
                    }
                }

                _currentTab = newTab;
                ft.Commit();
                SupportFragmentManager.ExecutePendingTransactions();
            }
        }

        protected virtual void FixupDataContext(TabInfo newTab)
        {
            var consumer = newTab.CachedFragment as IMvxDataConsumer;
            if (consumer == null)
                return;

            if (consumer.DataContext != newTab.ViewModel)
                consumer.DataContext = newTab.ViewModel;
        }

        protected virtual string FragmentJavaName(Type fragmentType)
        {
            return Class.FromType(fragmentType).Name;
        }

        public virtual void OnTabFragmentChanging(string tag, FragmentTransaction transaction)
        {
        }

        public override void OnAttachedToWindow()
        {
            base.OnAttachedToWindow();
            ViewModel?.Appearing();
        }

        public override void OnDetachedFromWindow()
        {
            base.OnDetachedFromWindow();
            ViewModel?.Disappearing(); // we don't have anywhere to get this info
            ViewModel?.Disappeared();
        }
    }
}