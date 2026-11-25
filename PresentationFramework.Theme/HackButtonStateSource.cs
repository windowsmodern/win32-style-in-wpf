using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace System.Windows.Controls
{
	/// <summary>
	/// 把外部元素（典型是 MenuItem）的交互状态接到 HackButton 的选图上。
	/// 只读取状态，不改变输入路由：事件仍由 source 接收。
	/// </summary>
	internal static class HackButtonStateSource
	{
		internal sealed class Tracker
		{
			internal UIElement Source;
			internal Action Update;
			internal bool MousePressed;
			internal Window Window;
			internal readonly EventHandler StateChanged;
			internal readonly MouseButtonEventHandler MouseDown;
			internal readonly MouseButtonEventHandler WindowUp;

			internal Tracker (UIElement source, Action update)
			{
				Source = source;
				Update = update;
				StateChanged = delegate { Update (); };
				MouseDown = OnMouseDown;
				WindowUp = OnWindowUp;
			}

			private void OnMouseDown (object sender, MouseButtonEventArgs e)
			{
				if (e.ChangedButton != MouseButton.Left)
					return;
				MousePressed = true;
				HookWindow ();
				Update ();
			}

			private void OnWindowUp (object sender, MouseButtonEventArgs e)
			{
				if (e.ChangedButton != MouseButton.Left)
					return;
				MousePressed = false;
				UnhookWindow ();
				Update ();
			}

			internal void HookWindow ()
			{
				if (Window != null)
					return;
				var window = Window.GetWindow (Source);
				if (window == null)
					return;
				Window = window;
				window.AddHandler (UIElement.PreviewMouseLeftButtonUpEvent, WindowUp, true);
			}

			internal void UnhookWindow ()
			{
				if (Window == null)
					return;
				Window.RemoveHandler (UIElement.PreviewMouseLeftButtonUpEvent, WindowUp);
				Window = null;
			}
		}

		private static readonly DependencyProperty TrackerProperty =
			DependencyProperty.RegisterAttached ("Tracker", typeof (Tracker), typeof (HackButtonStateSource), new PropertyMetadata (null));

		private static readonly DependencyPropertyDescriptor MouseOver =
			DependencyPropertyDescriptor.FromProperty (UIElement.IsMouseOverProperty, typeof (UIElement));
		private static readonly DependencyPropertyDescriptor KeyboardFocus =
			DependencyPropertyDescriptor.FromProperty (UIElement.IsKeyboardFocusWithinProperty, typeof (UIElement));
		private static readonly DependencyPropertyDescriptor EnabledFlag =
			DependencyPropertyDescriptor.FromProperty (UIElement.IsEnabledProperty, typeof (UIElement));
		private static readonly DependencyPropertyDescriptor PressedFlag =
			DependencyPropertyDescriptor.FromProperty (ButtonBase.IsPressedProperty, typeof (ButtonBase));

		internal static void Attach (UIElement owner, UIElement source, Action update)
		{
			Detach (owner);
			if (source == null)
				return;

			var tracker = new Tracker (source, update);
			owner.SetValue (TrackerProperty, tracker);
			MouseOver.AddValueChanged (source, tracker.StateChanged);
			KeyboardFocus.AddValueChanged (source, tracker.StateChanged);
			EnabledFlag.AddValueChanged (source, tracker.StateChanged);
			var button = source as ButtonBase;
			if (button != null)
				PressedFlag.AddValueChanged (button, tracker.StateChanged);
			source.AddHandler (UIElement.PreviewMouseLeftButtonDownEvent, tracker.MouseDown, true);
		}

		internal static void Detach (UIElement owner)
		{
			var tracker = owner == null ? null : owner.GetValue (TrackerProperty) as Tracker;
			if (tracker == null)
				return;
			MouseOver.RemoveValueChanged (tracker.Source, tracker.StateChanged);
			KeyboardFocus.RemoveValueChanged (tracker.Source, tracker.StateChanged);
			EnabledFlag.RemoveValueChanged (tracker.Source, tracker.StateChanged);
			var button = tracker.Source as ButtonBase;
			if (button != null)
				PressedFlag.RemoveValueChanged (button, tracker.StateChanged);
			tracker.Source.RemoveHandler (UIElement.PreviewMouseLeftButtonDownEvent, tracker.MouseDown);
			tracker.UnhookWindow ();
			owner.ClearValue (TrackerProperty);
		}

		private static Tracker Get (UIElement owner)
		{
			return owner == null ? null : owner.GetValue (TrackerProperty) as Tracker;
		}

		internal static bool IsPressed (UIElement owner)
		{
			var tracker = Get (owner);
			if (tracker == null)
				return false;
			var button = tracker.Source as ButtonBase;
			return button != null ? button.IsPressed : tracker.MousePressed;
		}

		internal static bool IsHovered (UIElement owner)
		{
			var tracker = Get (owner);
			return tracker != null && tracker.Source.IsMouseOver;
		}

		internal static bool IsFocused (UIElement owner)
		{
			var tracker = Get (owner);
			return tracker != null && tracker.Source.IsKeyboardFocusWithin;
		}

		internal static bool IsDisabled (UIElement owner)
		{
			var tracker = Get (owner);
			return tracker != null && !tracker.Source.IsEnabled;
		}
	}
}
