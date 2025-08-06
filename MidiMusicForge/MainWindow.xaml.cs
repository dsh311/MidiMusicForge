/*
 * Copyright (C) 2025 David S. Shelley <davidsmithshelley@gmail.com>
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License 
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 */

using System.Windows;
using System.Windows.Media;

namespace MidiMusicForge
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            tileEditorControl_Source.Loaded += (s, e) =>
            {
                //Set this, or zooming in will be smoothed out and you wont see pixels as easily
                RenderOptions.SetBitmapScalingMode(tileEditorControl_Source.TileSetImage, BitmapScalingMode.NearestNeighbor);
                RenderOptions.SetBitmapScalingMode(tileEditorControl_Source.TileSetImagePreview, BitmapScalingMode.NearestNeighbor);
            };
        }
    }
}