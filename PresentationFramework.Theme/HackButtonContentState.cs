using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls.Primitives;

namespace System.Windows.Controls
{
	/// <summary>被宿主按钮跟踪的按钮实现它，宿主状态变化时刷新自己的图。</summary>
	internal interface IHackButtonStateTarget
	{
		void RefreshFromOwner ();
	}

	/// <summary>
	/// 把 Content（ButtonBase）的悬停 / 按下 / 键盘焦点状态接进宿主按钮的选图逻辑，
	/// 同时让子按钮也读宿主的状态：两层互相跟随，输入仍归子按钮。
	/// </summary>
	internal static class HackButtonContentState
	{
		private sealed class Link
		{
			internal ButtonBase Owner;
		}

		private static readonly DependencyPropertyDescriptor MouseOver =
			DependencyPropertyDescriptor.FromProperty (UIElement.IsMouseOverProperty, typeof (UIElement));
		private static readonly DependencyPropertyDescriptor KeyboardFocus =
			DependencyPropertyDescriptor.FromProperty (UIElement.IsKeyboardFocusedProperty, typeof (UIElement));
		private static readonly DependencyPropertyDescriptor Pressed =
			DependencyPropertyDescriptor.FromProperty (ButtonBase.IsPressedProperty, typeof (ButtonBase));
		private static readonly DependencyProperty LinkProperty =
			DependencyProperty.RegisterAttached ("Link", typeof (Link), typeof (HackButtonContentState), new PropertyMetadata (null));

		internal static void Attach (ButtonBase content, EventHandler handler)
		{
			MouseOver.AddValueChanged (content, handler);
			KeyboardFocus.AddValueChanged (content, handler);
			Pressed.AddValueChanged (content, handler);
		}

		internal static void Detach (ButtonBase content, EventHandler handler)
		{
			MouseOver.RemoveValueChanged (content, handler);
			KeyboardFocus.RemoveValueChanged (content, handler);
			Pressed.RemoveValueChanged (content, handler);
		}

		/// <summary>让 content 在选择图片时也参考 owner 的状态。</summary>
		internal static void LinkToOwner (ButtonBase content, ButtonBase owner)
		{
			if (content == null)
				return;
			content.SetValue (LinkProperty, new Link { Owner = owner });
		}

		internal static void Unlink (ButtonBase content)
		{
			if (content != null)
				content.ClearValue (LinkProperty);
		}

		/// <summary>宿主状态变化时，让被跟踪的子按钮重选一次图（双向跟随）。</summary>
		internal static void RefreshLinkedContent (ButtonBase content)
		{
			var target = content as IHackButtonStateTarget;
			if (target != null && content.GetValue (LinkProperty) != null)
				target.RefreshFromOwner ();
		}

		private static Link GetLink (ButtonBase content)
		{
			return content == null ? null : content.GetValue (LinkProperty) as Link;
		}

		internal static bool OwnerPressed (ButtonBase content) { var link = GetLink (content); return link != null && link.Owner.IsPressed; }
		internal static bool OwnerHover (ButtonBase content) { var link = GetLink (content); return link != null && link.Owner.IsMouseOver; }
		internal static bool OwnerFocused (ButtonBase content) { var link = GetLink (content); return link != null && link.Owner.IsKeyboardFocused; }
		internal static bool OwnerDisabled (ButtonBase content) { var link = GetLink (content); return link != null && !link.Owner.IsEnabled; }
	}
}
