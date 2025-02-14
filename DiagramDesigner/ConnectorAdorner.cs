﻿using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls;
using DiagramDesigner.PathFinder;

namespace DiagramDesigner
{
    public class ConnectorAdorner : Adorner
    {
        private PathGeometry pathGeometry;
        private DesignerCanvas designerCanvas;
        private Connector sourceConnector;
        private Pen drawingPen;
        private bool _isArc = false;

        private DesignerItem hitDesignerItem;
        private DesignerItem HitDesignerItem
        {
            get { return hitDesignerItem; }
            set
            {
                if (hitDesignerItem != value)
                {
                    if (hitDesignerItem != null)
                        hitDesignerItem.IsDragConnectionOver = false;

                    hitDesignerItem = value;

                    if (hitDesignerItem != null)
                        hitDesignerItem.IsDragConnectionOver = true;
                }
            }
        }

        private Connector hitConnector;
        private Connector HitConnector
        {
            get { return hitConnector; }
            set
            {
                if (hitConnector != value)
                {
                    hitConnector = value;
                }
            }
        }

        public ConnectorAdorner(DesignerCanvas designer, Connector sourceConnector)
            : base(designer)
        {
            this.designerCanvas = designer;
            this.sourceConnector = sourceConnector;
            drawingPen = new Pen(Brushes.LightSlateGray, 1);
            drawingPen.LineJoin = PenLineJoin.Round;
            this.Cursor = Cursors.Cross;
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            var connections = designerCanvas.Children.OfType<Connection>();
            foreach (var connection in connections)
            {
                connection.IsHitTestVisible = true;
            }

            if (HitConnector != null && HitConnector.IsSinkConnector &&
                (!HitConnector.OnlyOneConnectionCanEnd || !HitConnector.Connections.Any(x => Equals(x.Sink, HitConnector)))
                && (!_isArc  ||   _isArc && !hitDesignerItem.HasArcLine))

            {
                Connector sourceConnector = null;
                Connector sinkConnector = null;
                //畫曲線
                if (_isArc)
                {
                    hitDesignerItem.HasArcLine = true;
                    sourceConnector = hitDesignerItem.SourceArcSegmentAnchor;
                    sinkConnector = hitDesignerItem.TargetArcSegmentAnchor;
                }
                else
                {
                    sourceConnector = this.sourceConnector;
                    sinkConnector = this.HitConnector;
                }

                if (_isArc)
                    designerCanvas.PathFinder = PathFinderTypes.StraightPathFinder;
                else
                    designerCanvas.PathFinder = PathFinderTypes.OrthogonalPathFinderWithoutMargin;

               Connection newConnection = designerCanvas.ConnectionGenerator(sourceConnector, sinkConnector,
                    designerCanvas.PathFinder, designerCanvas.GetPathText(), designerCanvas.GetPathColor(), _isArc);
                
                if (designerCanvas.ConnectionStyle != null)
                    newConnection.Style = designerCanvas.ConnectionStyle;

                this.designerCanvas.setZIndex(newConnection, designerCanvas.Children.Count);
                this.designerCanvas.Children.Add(newConnection);

                this.designerCanvas.raiseDesignerCanvasChanged();

                if (_isArc)
                    newConnection.RemoveArc += hitDesignerItem.OnRemovedArcConection;

                designerCanvas.PathFinder = PathFinderTypes.OrthogonalPathFinderWithoutMargin;
            }
            if (HitDesignerItem != null)
            {
                this.HitDesignerItem.IsDragConnectionOver = false;
            }

            if (this.IsMouseCaptured) this.ReleaseMouseCapture();

            AdornerLayer adornerLayer = AdornerLayer.GetAdornerLayer(this.designerCanvas);
            if (adornerLayer != null)
            {
                adornerLayer.Remove(this);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                if (!this.IsMouseCaptured) this.CaptureMouse();
                HitTesting(e.GetPosition(this));
                this.pathGeometry = GetPathGeometry(e.GetPosition(this));
                this.InvalidateVisual();
            }
            else
            {
                if (this.IsMouseCaptured) this.ReleaseMouseCapture();
            }
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            dc.DrawGeometry(null, drawingPen, this.pathGeometry);

            // without a background the OnMouseMove event would not be fired
            // Alternative: implement a Canvas as a child of this adorner, like
            // the ConnectionAdorner does.
            dc.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
        }

        private PathGeometry GetPathGeometry(Point position)
        {
            PathGeometry geometry = new PathGeometry();

            ConnectorOrientation targetOrientation;
            if (HitConnector != null)
                targetOrientation = HitConnector.Orientation;
            else
                targetOrientation = ConnectorOrientation.None;

            List<Point> pathPoints = PathFinderHelper.GetPathFinder(this.designerCanvas.PathFinder).GetConnectionLine(sourceConnector.GetInfo(), position, targetOrientation);

            if (pathPoints.Count > 0)
            {
                PathFigure figure = new PathFigure();
                figure.StartPoint = pathPoints[0];
                pathPoints.Remove(pathPoints[0]);
                figure.Segments.Add(new PolyLineSegment(pathPoints, true));
                geometry.Figures.Add(figure);
            }

            return geometry;
        }

        private void HitTesting(Point hitPoint)
        {
            bool hitConnectorFlag = false;

            DependencyObject hitObject = designerCanvas.InputHitTest(hitPoint) as DependencyObject;
            while (hitObject != null &&
                   (hitObject != sourceConnector.ParentDesignerItem || sourceConnector.ParentDesignerItem.IsLinkSelf) &&
                   hitObject.GetType() != typeof(DesignerCanvas))
            {
                if (hitObject is Connector)
                {
                    HitConnector = hitObject as Connector;
                    hitConnectorFlag = true;
                }

                if (hitObject is DesignerItem)
                {
                    HitDesignerItem = hitObject as DesignerItem;
                    if (hitObject == sourceConnector.ParentDesignerItem)
                        _isArc = true;

                    if (!hitConnectorFlag)
                        HitConnector = null;
                    return;
                }
                hitObject = VisualTreeHelper.GetParent(hitObject);
            }

            HitConnector = null;
            HitDesignerItem = null;
            _isArc = false;
        }
    }
}
