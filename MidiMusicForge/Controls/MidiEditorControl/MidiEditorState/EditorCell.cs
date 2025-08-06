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

namespace MidiMusicForge.Controls.MidiEditorControl.MidiEditorState
{
    public class EditorCell
    {
        public int TileId { get; set; }

        // Reference to the tile from the tileset (e.g., row and column in tileset grid)
        public int TilesetRow { get; set; }
        public int TilesetColumn { get; set; }

        // Rotation in degrees (e.g., 0, 90, 180, 270)
        public int Rotation { get; set; } = 0;

        // Optional pixel offset when drawing the tile
        public int OffsetX { get; set; } = 0;
        public int OffsetY { get; set; } = 0;

        // Optional: Flip flags
        public bool FlipHorizontal { get; set; } = false;
        public bool FlipVertical { get; set; } = false;

        // Optional: Is this cell empty?
        public bool IsEmpty { get; set; } = true;

        // Optional constructor
        public EditorCell(int tilesetRow, int tilesetColumn)
        {
            IsEmpty = true;
            TilesetRow = tilesetRow;
            TilesetColumn = tilesetColumn;
        }
    }
}
