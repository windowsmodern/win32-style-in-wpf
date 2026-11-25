using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace System.Windows.Controls
{
	/// <summary>HackImage 的附加属性过渡：To 变化时自动从当前 Source 过渡到新图（null 视为透明）；完成后以 SetCurrentValue 更新 Source（保留绑定），供 DataTrigger / Interaction.Triggers 使用。</summary>
	public static class HackImageTransitions
	{
		public static readonly DependencyProperty ToProperty = DependencyProperty.RegisterAttached ("To", typeof (ImageSource), typeof (HackImageTransitions), new PropertyMetadata (null, OnToChanged));
		public static readonly DependencyProperty DurationProperty = DependencyProperty.RegisterAttached ("Duration", typeof (Duration), typeof (HackImageTransitions), new PropertyMetadata (new Duration (TimeSpan.FromMilliseconds (500))));
		public static readonly DependencyProperty EasingProperty = DependencyProperty.RegisterAttached ("Easing", typeof (IEasingFunction), typeof (HackImageTransitions), new PropertyMetadata (null));
		private static readonly DependencyPropertyKey IsRunningPropertyKey = DependencyProperty.RegisterAttachedReadOnly ("IsRunning", typeof (bool), typeof (HackImageTransitions), new PropertyMetadata (false));
		public static readonly DependencyProperty IsRunningProperty = IsRunningPropertyKey.DependencyProperty;
		private static readonly DependencyProperty TransitionProperty = DependencyProperty.RegisterAttached ("Transition", typeof (HackImageTransition), typeof (HackImageTransitions), new PropertyMetadata (null));
		public static ImageSource GetTo (DependencyObject d) { return (ImageSource)d.GetValue (ToProperty); }
		public static void SetTo (DependencyObject d, ImageSource value)
		{
			// null 是附加属性的默认值：当 To 已经是 null 时，SetValue 不会触发属性变更回调，
			// 但元素上可能还留着上一张图（例如 Normal 为 null、刚按 Hover 直接赋值过一次）。
			// 这种情况下必须手动启动一次"淡出到透明"，否则图会残留在控件上。
			bool alreadyNull = d.GetValue (ToProperty) == null;
			d.SetValue (ToProperty, value);
			if (value == null && alreadyNull) Begin (d, null);
		}
		public static Duration GetDuration (DependencyObject d) { return (Duration)d.GetValue (DurationProperty); }
		public static void SetDuration (DependencyObject d, Duration value) { d.SetValue (DurationProperty, value); }
		public static IEasingFunction GetEasing (DependencyObject d) { return (IEasingFunction)d.GetValue (EasingProperty); }
		public static void SetEasing (DependencyObject d, IEasingFunction value) { d.SetValue (EasingProperty, value); }
		public static bool GetIsRunning (DependencyObject d) { return (bool)d.GetValue (IsRunningProperty); }
		private static void OnToChanged (DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			Begin (d, e.NewValue as ImageSource);
		}
		private static void Begin (DependencyObject d, ImageSource newValue)
		{
			var img = d as HackImage;
			if (img == null) return;
			BitmapSource midFrame = null;
			var old = d.GetValue (TransitionProperty) as HackImageTransition;
			// 已经朝同一个目标在走（例如连续两次 SetTo(null) 的淡出），不要重启动画。
			if (old != null && GetIsRunning (img) && ReferenceEquals (GetTo (img), newValue)) return;
			if (old != null)
			{
				midFrame = old.GetCurrentFrame ();
				old.Dispose ();
				d.SetValue (TransitionProperty, null);
				d.SetValue (IsRunningPropertyKey, false);
			}
			var to = newValue as BitmapSource;
			var from = midFrame ?? (img.Source as BitmapSource);
			if (from == null && to == null) return;
			if (from != null && ReferenceEquals (from, to)) return;
			if (to != null && (to.PixelWidth == 0 || to.PixelHeight == 0)) { img.SetCurrentValue (HackImage.SourceProperty, to); return; }
			// 不可切片的图无法参与过渡，直接赋值（IsSourceShrink=true 时任何尺寸都可切片）
			if (!CanSlice (img, from) || !CanSlice (img, to)) { img.SetCurrentValue (HackImage.SourceProperty, to); return; }
			var trans = new HackImageTransition (img, from, to, GetDuration (img).TimeSpan);
			trans.Easing = GetEasing (img);
			trans.Completed += OnCompleted;
			d.SetValue (TransitionProperty, trans);
			img.SetValue (IsRunningPropertyKey, true);
			trans.Start ();
		}
		/// <summary>取消正在推进的过渡，不应用目标图；切片由调用方自行刷新。</summary>
		internal static void Cancel (HackImage img)
		{
			var trans = img.GetValue (TransitionProperty) as HackImageTransition;
			img.SetValue (TransitionProperty, null);
			img.SetValue (IsRunningPropertyKey, false);
			if (trans != null) trans.Abort ();
		}
		// 检查一张图在当前 HackThickness / IsSourceShrink 下能否切片；null 视为可切片（透明）
		private static bool CanSlice (HackImage img, BitmapSource bmp)
		{
			if (bmp == null) return true;
			if (img.IsSourceShrink) return true;
			var h = img.HackThickness;
			int l = HackImage.ToNonNegativeInt (h.Left), t = HackImage.ToNonNegativeInt (h.Top);
			int r = HackImage.ToNonNegativeInt (h.Right), b = HackImage.ToNonNegativeInt (h.Bottom);
			return (long)l + r <= bmp.PixelWidth && (long)t + b <= bmp.PixelHeight;
		}
		// 过渡完成：复位 IsRunning、清 Transition、Dispose 释放像素缓冲
		private static void OnCompleted (object sender, EventArgs e)
		{
			var trans = sender as HackImageTransition;
			if (trans == null) return;
			var target = trans.Target;
			target.SetValue (IsRunningPropertyKey, false);
			target.SetValue (TransitionProperty, null);
			trans.Dispose ();
		}
	}
}