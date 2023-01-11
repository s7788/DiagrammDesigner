﻿using System;
using System.ComponentModel;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using DiagramDesigner.PathFinder;

namespace DiagramDesigner
{
    public partial class DesignerCanvas : Canvas
    {
        private Point? rubberbandSelectionStartPoint = null;

        private SelectionService selectionService;
        public SelectionService SelectionService
        {
            get
            {
                if (selectionService == null)
                    selectionService = new SelectionService(this);

                return selectionService;
            }
        }

        public bool SelectionNeedsCtrl { get; set; }

        public PathFinderTypes PathFinder { get; set; }

        public int SelectionLayer
        {
            get
            {
                var d = SelectedItems.FirstOrDefault(x => x is DesignerItem) as DesignerItem;
                if (d != null)
                    return d.Layer;
                return 0;
            }
            set
            {
                foreach (var selectedItem in SelectedItems)
                {
                    var d = selectedItem as DesignerItem;
                    if (d != null)
                        d.Layer = value;
                }
                updateVisibleDesigneritems();
                OnPropertyChanged("SelectionLayer");
            }
        }

        public event SelectionChangedEventHandler SelectionChanged;

        public void ClearSelection()
        {
            selectionService.ClearSelection();
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Source == this && (!SelectionNeedsCtrl || Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)))
            {
                // in case that this click is the start of a 
                // drag operation we cache the start point
                this.rubberbandSelectionStartPoint = new Point?(e.GetPosition(this));

                // if you click directly on the canvas all 
                // selected items are 'de-selected'
                SelectionService.ClearSelection();
                Focus();
                e.Handled = true;
            }
        }

        protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseLeftButtonUp(e);

            string storedDiagram = this.StoreDiagram().ToString();
            if (storedDiagram != this.diagramState.Value)
            {
                this.diagramState.SaveState();
                this.diagramState.Value = storedDiagram;
            }
        }

        public Style ConnectionStyle { get; set; }

        internal event Action<double> ScaleChanged;
        public static readonly DependencyProperty PathTextProperty
            = DependencyProperty.Register("PathText", typeof(string), typeof(Canvas), null);
        public string PathText
        {
            get => (string)this.GetValue(PathTextProperty);
            set
            {
                this.SetValue(PathTextProperty, value);
            }
        }

        void DesignerCanvas_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (this.LayoutTransform is ScaleTransform)
            {
                if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                {
                    var scale = e.Delta * 0.002 + ((ScaleTransform)this.LayoutTransform).ScaleX;
                    scale = scale < 0.05 ? 0.05 : scale;
                    scale = scale > 5 ? 5 : scale;
                    ((ScaleTransform)this.LayoutTransform).ScaleX = scale;
                    ((ScaleTransform)this.LayoutTransform).ScaleY = scale;
                    e.Handled = true;

                    if (ScaleChanged != null)
                        ScaleChanged(scale * 100);
                }
            }
        }

        private void DesignerCanvas_RequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
        {
            e.Handled = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            // if mouse button is not pressed we have no drag operation, ...
            if (e.LeftButton != MouseButtonState.Pressed && (!SelectionNeedsCtrl || Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)))
                this.rubberbandSelectionStartPoint = null;

            // ... but if mouse button is pressed and start
            // point value is set we do have one
            if (this.rubberbandSelectionStartPoint.HasValue)
            {
                // create rubberband adorner
                AdornerLayer adornerLayer = AdornerLayer.GetAdornerLayer(this);
                if (adornerLayer != null)
                {
                    RubberbandAdorner adorner = new RubberbandAdorner(this, rubberbandSelectionStartPoint);
                    if (adorner != null)
                    {
                        adornerLayer.Add(adorner);
                    }
                }
                e.Handled = true;
            }

        }

        public virtual object CreateInstanceFromDragObject(DragObject dragObject)
        {
            return Activator.CreateInstance(dragObject.ObjectType);
        }

        protected override void OnDrop(DragEventArgs e)
        {
            base.OnDrop(e);
            DragObject dragObject = e.Data.GetData(typeof(DragObject)) as DragObject;
            if (dragObject != null) // && !String.IsNullOrEmpty(dragObject.Xaml))
            {
                //DesignerItem newItem = null;
                Object content = CreateInstanceFromDragObject(dragObject);

                if (content != null)
                {
                    Point position = e.GetPosition(this);

                    var x = Math.Max(0, position.X);
                    var y = Math.Max(0, position.Y);

                    if (dragObject.DesiredSize.HasValue)
                    {
                        x = Math.Max(0, position.X - dragObject.DesiredSize.Value.Width / 2);
                        y = Math.Max(0, position.Y - dragObject.DesiredSize.Value.Height / 2);
                    }

                    if (this.UseRaster)
                    {
                        x = Math.Round(x / Raster, 0) * Raster;
                        y = Math.Round(y / Raster, 0) * Raster;
                    }

                    AddDesignerItem(content as FrameworkElement, new Point(x, y), dragObject.DesiredSize, 0, dragObject.InsertInBackground);
                }

                e.Handled = true;
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.Key == Key.Left || e.Key == Key.Right || e.Key == Key.Up || e.Key == Key.Down)
            {
                object originalSource = e.OriginalSource;
                if (originalSource is DesignerItem)
                {
                    var movementViewModel = new MovementViewModel();
                    e.Handled = true;

                    var item = originalSource as DesignerItem;

                    if (item.ParentID != Guid.Empty)
                    {
                        var groupMembers = selectionService.GetGroupMembers(item).OfType<DesignerItem>();

                        bool leftConstrained = groupMembers.Any(designerItem => Canvas.GetLeft(designerItem) == 0);
                        bool topConstrained = groupMembers.Any(designerItem => Canvas.GetTop(designerItem) == 0);
                        if (e.Key == Key.Left && leftConstrained ||
                            e.Key == Key.Up && topConstrained)
                        {
                            return;
                        }
                        foreach (DesignerItem designerItem in groupMembers)
                        {
                            movementViewModel.HandleKeyDown(e, designerItem);
                        }
                    }
                    else
                    {
                        movementViewModel.HandleKeyDown(e, item);
                    }
                }
            }
        }

        public delegate Connection ConnectionGeneratorDelegate(Connector source, Connector sink, PathFinderTypes pathFinderType, string text);
        public ConnectionGeneratorDelegate ConnectionGenerator { get; set; }

        public delegate void DesignerCanvasChangedDelegate();
        public event DesignerCanvasChangedDelegate DesignerCanvasChanged;

        public delegate void DesignerItemAddedDelegate(object item, DesignerItem designerItem);
        public event DesignerItemAddedDelegate ItemAdded;

        public delegate void DesignerItemLayerChangedDelegate(object item, DesignerItem designerItem, int newLayer);
        public event DesignerItemLayerChangedDelegate ItemLayerChanged;

        public delegate void DesignerItemRemovedDelegate(object item, DesignerItem designerItem);
        public event DesignerItemRemovedDelegate ItemRemoved;

        private bool _useRaster = true;
        public bool UseRaster
        {
            get { return _useRaster; }
            set { _useRaster = value; }
        }

        private int _raster = 5;
        public int Raster
        {
            get { return _raster; }
            set { _raster = value; }
        }
        private string _pathText;
        internal void raiseLayerChanged(DesignerItem item, int layer)
        {
            var e = ItemLayerChanged;
            if (e != null)
                e(this, item, layer);
        }

        private Dictionary<int, bool> visibleLayers = new Dictionary<int, bool>();

        private void raiseDesignerItemAdded(object item, DesignerItem designerItem)
        {
            var x = ItemAdded;
            if (x != null)
                x(item, designerItem);

            raiseDesignerCanvasChanged();
        }

        internal void raiseDesignerCanvasChanged()
        {
            var x = DesignerCanvasChanged;
            if (x != null)
                x();
        }

        private void raiseDesignerItemRemoved(object item, DesignerItem designerItem)
        {
            var x = ItemRemoved;
            if (x != null)
                x(item, designerItem);

            raiseDesignerCanvasChanged();
        }

        public void SendItemsToBack(IEnumerable<ISelectable> items)
        {
            List<UIElement> selectionSorted = (from item in items
                                               orderby getZIndex(item as UIElement) ascending
                                               select item as UIElement).ToList();

            List<UIElement> childrenSorted = (from UIElement item in this.Children
                                              orderby getZIndex(item as UIElement) ascending
                                              select item as UIElement).ToList();
            int i = 0;
            int j = 0;
            foreach (UIElement item in childrenSorted)
            {
                if (selectionSorted.Contains(item))
                {
                    int idx = getZIndex(item);
                    setZIndex(item, j++);

                }
                else
                {
                    setZIndex(item, selectionSorted.Count + i++);
                }
            }
        }

        public DesignerItem AddDesignerItem(FrameworkElement item, Point position, Size? size, int layer = 0, bool insertInBackground = false, Guid? itemGuid = null)
        {
            DesignerItem newItem = new DesignerItem();

            if (itemGuid != null)
                newItem.ID = itemGuid.Value;

            newItem.Content = item;
            newItem.Layer = layer;
            if (size.HasValue)
            {
                newItem.Width = size.Value.Width;
                newItem.Height = size.Value.Height;
            }

            DesignerCanvas.SetLeft(newItem, position.X);
            DesignerCanvas.SetTop(newItem, position.Y);

            //Canvas.SetZIndex(newItem, this.Children.Count);
            newItem.ZIndex = this.Children.Count;

            if (insertInBackground)
            {
                newItem.ZIndex = 0;
                this.Children.Insert(0, newItem);
            }
            else
                this.Children.Add(newItem);
            SetConnectorDecoratorTemplate(newItem);

            //update selection
            //this.SelectionService.SelectItem(newItem);
            //newItem.Focus();

            raiseDesignerItemAdded(item, newItem);

            bool layerVisible = false;
            if (!visibleLayers.TryGetValue(layer, out layerVisible) || layerVisible)
            {
                item.Visibility = System.Windows.Visibility.Visible;
            }
            else
            {
                item.Visibility = System.Windows.Visibility.Hidden;
            }
            //updateVisibleDesigneritems();

            return newItem;
        }

        private void OnDesignerItemContentPropertyChanged(object sender, PropertyChangedEventArgs propertyChangedEventArgs)
        {
            raiseDesignerCanvasChanged();
        }

        internal void updateVisibleDesigneritems()
        {
            bool layerVisible;
            foreach (FrameworkElement child in this.Children)
            {
                if (child is DesignerItem)
                {
                    var layer = ((DesignerItem)child).Layer;

                    if (!visibleLayers.TryGetValue(layer, out layerVisible) || layerVisible)
                    {
                        child.Visibility = System.Windows.Visibility.Visible;
                    }
                    else if (visibleLayers.TryGetValue(layer, out layerVisible) && !layerVisible)
                    {
                        child.Visibility = System.Windows.Visibility.Hidden;
                    }
                }
                else if (child is Connection)
                {
                    var connection = child as Connection;
                    if (connection.Source == null || connection.Sink == null)
                    {
                        connection.Visibility = System.Windows.Visibility.Visible;
                    }
                    else if ((!visibleLayers.TryGetValue(connection.Source.ParentDesignerItem.Layer, out layerVisible) || layerVisible) && (!visibleLayers.TryGetValue(connection.Sink.ParentDesignerItem.Layer, out layerVisible) || layerVisible))
                    {
                        connection.Visibility = System.Windows.Visibility.Visible;
                    }
                    else
                    {
                        connection.Visibility = System.Windows.Visibility.Hidden;
                    }
                }
            }

            if (SelectedItems.Any(x => ((FrameworkElement)x).Visibility == System.Windows.Visibility.Hidden))
                ClearSelection();
        }

        public void SwitchLayerVisibility(int layer, bool visible)
        {
            if (!visibleLayers.ContainsKey(layer))
                visibleLayers.Add(layer, visible);
            visibleLayers[layer] = visible;

            updateVisibleDesigneritems();
        }

        private List<DesignerItem> designerItems;


        protected override Size MeasureOverride(Size constraint)
        {
            Size size = new Size();

            foreach (UIElement element in this.InternalChildren)
            {
                double left = Canvas.GetLeft(element);
                double top = Canvas.GetTop(element);
                left = double.IsNaN(left) ? 0 : left;
                top = double.IsNaN(top) ? 0 : top;

                //measure desired size for each child
                element.Measure(constraint);

                Size desiredSize = element.DesiredSize;
                if (!double.IsNaN(desiredSize.Width) && !double.IsNaN(desiredSize.Height))
                {
                    size.Width = Math.Max(size.Width, left + desiredSize.Width);
                    size.Height = Math.Max(size.Height, top + desiredSize.Height);
                }
            }
            // add margin 
            size.Width += 10;
            size.Height += 10;

            return size;
        }

        private void SetConnectorDecoratorTemplate(DesignerItem item)
        {
            if (item.ApplyTemplate() && item.Content is UIElement)
            {
                ControlTemplate template = DesignerItem.GetConnectorDecoratorTemplate(item.Content as UIElement);
                Control decorator = item.Template.FindName("PART_ConnectorDecorator", item) as Control;
                if (decorator != null && template != null)
                    decorator.Template = template;
            }
        }

        public string GetPathText()
        {
            return PathText;
        }
    }
}
