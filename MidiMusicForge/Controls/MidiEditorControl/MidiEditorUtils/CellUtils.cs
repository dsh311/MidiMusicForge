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

using MidiMusicForge.Controls.MidiEditorControl.MidiEditorState;

namespace MidiMusicForge.Controls.MidiEditorControl.MidiEditorUtils
{
    public static class CellUtils
    {
        public class EditorCellArrayWrapper
        {
            public int Width { get; set; }
            public int Height { get; set; }
            public int TileWidth { get; set; }
            public int TileHeight { get; set; }
            public int TileId { get; set; }
            public EditorCell[][] Cells { get; set; } = default!;
        }

        public static EditorCell[,] CreateEmptyTileMapArray(int numGridRows, int numGridCols)
        {
            var array = new EditorCell[numGridRows, numGridCols];

            for (int y = 0; y < numGridRows; y++)
            {
                for (int x = 0; x < numGridCols; x++)
                {
                    EditorCell newGridCell = new EditorCell(-1, -1);
                    int tileId = ((y * numGridCols) + x) + 1;
                    newGridCell.TileId = tileId;

                    array[y, x] = newGridCell;
                }
            }

            return array;
        }

    }
}
