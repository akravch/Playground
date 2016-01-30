using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace Playground
{
    /// <summary>
    /// A polyline that has an ability to display animated moving dots along itself.
    /// </summary>
    public class AnimatedPolyline : Shape
    {
        #region Instance Fields and Constants
        
        private const double DefaultDotRadius = 5.0;
        private const double DefaultDotInterval = 150.0;
        private const double DefaultDotSpeed = DefaultDotInterval / 1000.0;

        private static readonly Pen EmptyPen = new Pen(); // No need to recreate an empty pen on each render action.

        private Geometry polylineGeometry;
        private double length;

        #endregion

        #region Dynamic Properties

        /// <summary>
        /// Identifies the <see cref="Points"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty PointsProperty = DependencyProperty.Register(
            "Points", typeof(PointCollection), 
            typeof(AnimatedPolyline),
            new FrameworkPropertyMetadata(new PointCollection().GetAsFrozen(), FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>
        /// Gets or sets a collection that contains the vertex points of the AnimatedPolyline.
        /// </summary>
        public PointCollection Points
        {
            get { return (PointCollection)GetValue(PointsProperty); }
            set { SetValue(PointsProperty, value); }
        }

        /// <summary>
        /// Identifies the <see cref="FillRule"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty FillRuleProperty = DependencyProperty.Register(
            "FillRule",
            typeof(FillRule),
            typeof(AnimatedPolyline),
            new FrameworkPropertyMetadata(FillRule.EvenOdd, FrameworkPropertyMetadataOptions.AffectsRender),
            IsFillRuleValid);

        /// <summary>
        /// Gets or sets a System.Windows.Media.FillRule enumeration that specifies how the
        /// interior fill of the shape is determined.
        /// </summary>
        public FillRule FillRule
        {
            get { return (FillRule)GetValue(FillRuleProperty); }
            set { SetValue(FillRuleProperty, value); }
        }

        /// <summary>
        /// Identifies the <see cref="IsAnimated"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty IsAnimatedProperty = DependencyProperty.Register(
            "IsAnimated", 
            typeof(bool), 
            typeof(AnimatedPolyline), 
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>
        /// Gets or sets a value identifying whether the instance 
        /// will display animated moving points along itself.
        /// </summary>
        public bool IsAnimated
        {
            get { return (bool) GetValue(IsAnimatedProperty); }
            set { SetValue(IsAnimatedProperty, value); }
        }

        /// <summary>
        /// Identifies the <see cref="DotInterval"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty DotIntervalProperty = DependencyProperty.Register(
            "DotInterval", 
            typeof(double), 
            typeof(AnimatedPolyline), 
            new FrameworkPropertyMetadata(DefaultDotInterval, FrameworkPropertyMetadataOptions.AffectsRender),
            IsPositiveDouble);

        /// <summary>
        /// Gets or sets the distance between the moving dots in pixels.
        /// Must be positive.
        /// </summary>
        public double DotInterval
        {
            get { return (double) GetValue(DotIntervalProperty); }
            set { SetValue(DotIntervalProperty, value); }
        }

        /// <summary>
        /// Identifies the <see cref="DotSpeed"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty DotSpeedProperty = DependencyProperty.Register(
            "DotSpeed", 
            typeof (double), 
            typeof (AnimatedPolyline), 
            new FrameworkPropertyMetadata(DefaultDotSpeed, FrameworkPropertyMetadataOptions.AffectsRender),
            IsPositiveDouble);

        /// <summary>
        /// Gets or sets the speed of a moving dot in pixels per second.
        /// Must be positive.
        /// </summary>
        public double DotSpeed
        {
            get { return (double) GetValue(DotSpeedProperty); }
            set { SetValue(DotSpeedProperty, value); }
        }

        /// <summary>
        /// Identifies the <see cref="DotRadius"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty DotRadiusProperty = DependencyProperty.Register(
            "DotRadius", 
            typeof (double), 
            typeof (AnimatedPolyline), 
            new FrameworkPropertyMetadata(DefaultDotRadius));

        /// <summary>
        /// Gets or sets the dot radius.
        /// Must be positive.
        /// </summary>
        public double DotRadius
        {
            get { return (double) GetValue(DotRadiusProperty); }
            set { SetValue(DotRadiusProperty, value); }
        }

        #endregion

        #region Overrides

        /// <summary>
        /// <see cref="Shape.DefiningGeometry"/>
        /// </summary>
        protected override Geometry DefiningGeometry
        {
            get { return polylineGeometry; }
        }

        /// <summary>
        /// <see cref="Shape.MeasureOverride"/>
        /// </summary>
        protected override Size MeasureOverride(Size constraint)
        {
            // Update a geometry when the polyline gets restructured.
            CacheDefiningGeometry();

            return base.MeasureOverride(constraint);
        }

        /// <summary>
        /// <see cref="Shape.OnRender"/>
        /// </summary>
        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            if (IsAnimated)
            {
                var pathGeometry = polylineGeometry as PathGeometry;

                if (pathGeometry != null)
                {
                    var startPoint = Points.FirstOrDefault(); // Each dot starts moving from the first point of the polyline.
                    var anumationDuration = new Duration(TimeSpan.FromSeconds(length / DotSpeed));
                    var relativeDelay = DotInterval / DotSpeed; // The time span between the start moments of the animation for two sequential dots.
                    var dotCount = (int)(length / DotInterval);

                    for (var i = 0; i < dotCount; i++)
                    {
                        var animationDelay = TimeSpan.FromSeconds(i * relativeDelay);
                        var centerAnimation = new PointAnimationUsingPath // Animate the center of the dot to move along the polyline.
                        {
                            PathGeometry = pathGeometry,
                            Duration = anumationDuration,
                            RepeatBehavior = RepeatBehavior.Forever,
                            BeginTime = animationDelay // Delaying animation prevents all dots from starting moving at the same time.
                        };

                        // A hacky way to hide the dot while it's waiting on the start - just set its radius to zero and restore back when it starts moving.
                        var radiusAnimation = new DoubleAnimation(0.0, DotRadius, TimeSpan.Zero)
                        {
                            BeginTime = animationDelay
                        };
                        var centerAnimationClock = centerAnimation.CreateClock();
                        var radiusAnimationClock = radiusAnimation.CreateClock();

                        // Draw the dot at the beginning of the line. Will have the same fill as line stroke and no border.
                        drawingContext.DrawEllipse(Stroke, EmptyPen, startPoint, centerAnimationClock, 0.0, radiusAnimationClock, 0.0, radiusAnimationClock);
                    }
                }
            }
        }

        #endregion

        #region Private Methods

        private void CacheDefiningGeometry()
        {
            polylineGeometry = Geometry.Empty;
            length = 0.0;
            var pointCollection = Points;
            
            if (pointCollection != null)
            {
                var pathFigure = new PathFigure();

                if (pointCollection.Count > 0)
                {
                    var previousPoint = pointCollection[0];
                    pathFigure.StartPoint = previousPoint;

                    if (pointCollection.Count > 1)
                    {
                        var array = new Point[pointCollection.Count - 1];

                        for (var i = 1; i < pointCollection.Count; i++)
                        {
                            var point = pointCollection[i];
                            array[i - 1] = point;
                            length += (point - previousPoint).Length; // Cache the length so no need to go through all the points when calculating stuff for dots.
                            previousPoint = point;
                        }

                        pathFigure.Segments.Add(new PolyLineSegment(array, true));
                    }
                }

                var pathGeometry = new PathGeometry
                {
                    Figures = { pathFigure },
                    FillRule = FillRule
                };

                polylineGeometry = pathGeometry.Bounds == Rect.Empty ? Geometry.Empty : pathGeometry;
            }
        }

        private static bool IsFillRuleValid(object value)
        {
            switch ((FillRule)value)
            {
                case FillRule.EvenOdd:
                case FillRule.Nonzero:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsPositiveDouble(object value)
        {
            return (double)value > 0.0;
        }

        #endregion
    }
}
