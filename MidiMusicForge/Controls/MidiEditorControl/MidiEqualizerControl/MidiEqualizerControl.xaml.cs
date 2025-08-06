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

using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace MidiMusicForge.Controls.MidiEditorControl.MidiEqualizerControl
{
    /// <summary>
    /// Interaction logic for MidiEqualizerControl.xaml
    /// </summary>
    public partial class MidiEqualizerControl : UserControl
    {

        public class ProcessedNote
        {
            public Melanchall.DryWetMidi.Interaction.Note Note { get; set; }
            public double StartTimeMs { get; set; }
            public double EndTimeMs { get; set; }
            public double DurationMs { get; set; }
            public FourBitNumber Channel { get; set; } // Corrected type: FourBitNumber
        }


        // Define frequency band ranges using MIDI Note Numbers (0-127)
        // These are suggestions; you can adjust them based on common instrument ranges
        // and what looks good visually for your MIDI files.
        private static readonly int[] BAND_BOUNDARIES = new int[]
        {
            3,   // Band 0:  Notes 0-3
            7,   // Band 1:  Notes 4-7
            11,  // Band 2:  Notes 8-11
            15,  // Band 3:  Notes 12-15
            19,  // Band 4:  Notes 16-19
            23,  // Band 5:  Notes 20-23
            27,  // Band 6:  Notes 24-27
            31,  // Band 7:  Notes 28-31
            35,  // Band 8:  Notes 32-35
            39,  // Band 9:  Notes 36-39
            43,  // Band 10: Notes 40-43
            47,  // Band 11: Notes 44-47
            51,  // Band 12: Notes 48-51
            55,  // Band 13: Notes 52-55
            59,  // Band 14: Notes 56-59
            63,  // Band 15: Notes 60-63 (Includes Middle C: 60)
            67,  // Band 16: Notes 64-67
            71,  // Band 17: Notes 68-71
            75,  // Band 18: Notes 72-75
            79,  // Band 19: Notes 76-79
            83,  // Band 20: Notes 80-83
            87,  // Band 21: Notes 84-87
            91,  // Band 22: Notes 88-91
            95,  // Band 23: Notes 92-95
            99,  // Band 24: Notes 96-99
            103, // Band 25: Notes 100-103
            107, // Band 26: Notes 104-107
            111, // Band 27: Notes 108-111
            115, // Band 28: Notes 112-115
            119, // Band 29: Notes 116-119
            127  // Band 30: Notes 120-127 (This last band is 8 notes to cover the remainder)
        };

        // Array to store current intensity for each band
        private double[] _currentBandIntensities;
        // New array to track the *peak* intensity for each band
        private double[] _peakBandIntensities;

        // Array to hold references to the Rectangle UI elements
        private Rectangle[] _eqBars = Array.Empty<Rectangle>();


        // Parameters for peak decay
        private const double PEAK_DECAY_RATE = 0.99; // How much the peak value decays each tick (e.g., 0.99 for slow decay)
        private const double MIN_PEAK_THRESHOLD = 0.5; // Prevent peak from going to zero, adjust as needed

        List<ProcessedNote> _allProcessedNotes = new List<ProcessedNote>();

        public MidiEqualizerControl()
        {
            InitializeComponent();

            eqCanvas.Loaded += (s, e) =>
            {
                redrawEqualizer();
            };

            // -----------
            // Initialize arrays based on the number of bands
            _currentBandIntensities = new double[BAND_BOUNDARIES.Length];


            _peakBandIntensities = new double[BAND_BOUNDARIES.Length]; // Initialize the peak array
            // Initialize peak intensities to a small non-zero value to avoid division by zero
            for (int i = 0; i < BAND_BOUNDARIES.Length; i++)
            {
                _peakBandIntensities[i] = MIN_PEAK_THRESHOLD; // Or some other reasonable starting value
                                                              // ... (if you're assigning XAML elements to _eqBars) ...
            }
            // ------------
        }


        public void PreprocessMidiFileForEqualizer(MidiFile MidiFileLoaded, TempoMap MidiFileTempoMapLoaded)
        {
            _allProcessedNotes = new List<ProcessedNote>();

            foreach (var note in MidiFileLoaded.GetNotes())
            {
                var startTime = TimeConverter.ConvertTo<MetricTimeSpan>(note.Time, MidiFileTempoMapLoaded);
                var duration = Melanchall.DryWetMidi.Interaction.LengthConverter.ConvertTo<MetricTimeSpan>(note.Length, note.Time, MidiFileTempoMapLoaded);
                FourBitNumber channel = note.Channel; // Directly access the Channel property of the Note object!

                _allProcessedNotes.Add(new ProcessedNote
                {
                    Note = note,
                    StartTimeMs = startTime.TotalMilliseconds,
                    EndTimeMs = startTime.TotalMilliseconds + duration.TotalMilliseconds,
                    DurationMs = duration.TotalMilliseconds,
                    Channel = channel // Now correctly assigned
                });
            }
        }

        private void redrawEqualizer()
        {
            _eqBars = new Rectangle[BAND_BOUNDARIES.Length];

            eqCanvas.Children.Clear();

            double canWidth = eqCanvas.ActualWidth;
            double barWidth = (canWidth / BAND_BOUNDARIES.Length) - 1; // -1 for spacer that comes after
            double barSpacing = 1;
            for (int i = 0; i < BAND_BOUNDARIES.Length; i++)
            {
                Rectangle newBar = new Rectangle
                {
                    Width = barWidth,
                    Height = 0,
                    Stroke = Brushes.White,
                    StrokeThickness = 1,
                    Fill = new LinearGradientBrush
                    {
                        StartPoint = new Point(0.5, 1), // bottom center
                        EndPoint = new Point(0.5, 0),   // top center
                        GradientStops = new GradientStopCollection
                        {
                            new GradientStop(Color.FromRgb(30, 144, 255), 0.0),  // bottom
                            new GradientStop(Color.FromRgb(135, 206, 250), 0.6), // light blue
                            new GradientStop(Colors.White, 1.0)                  // top
                        }
                    }
                };

                Canvas.SetLeft(newBar, (barWidth + barSpacing) * i);
                Canvas.SetTop(newBar, 0); // Initial position at bottom
                eqCanvas.Children.Add(newBar);
                _eqBars[i] = newBar;
            }
        }
        public void ZeroEqualizerBars()
        {
            // Check if we are on the UI thread. If not, invoke this method on the UI thread.
            if (!Dispatcher.CheckAccess())
            {
                // Recursively call this method on the UI thread.
                Dispatcher.Invoke(() => ZeroEqualizerBars());
                return;
            }
            // The rest of this code is now guaranteed to run on the UI thread.
            if (_eqBars != null)
            {
                // Loop through each existing Rectangle in our array
                for (int i = 0; i < _eqBars.Length; i++)
                {
                    Rectangle bar = _eqBars[i];
                    // Set the height to 0
                    bar.Height = 0;
                    // Adjust the top position to keep the bar anchored at the bottom
                    Canvas.SetTop(bar, eqCanvas.ActualHeight);
                }
            }
        }

        private void eqCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            redrawEqualizer();
        }



        public void renderEqualizer(double currentTimeMs, double windowEndMs)
        {
            if (_allProcessedNotes == null)
            {
                return;
            }
            // --- Equalizer Visualization Logic ---

            //double currentTimeMs = playback.GetCurrentTime<MetricTimeSpan>().TotalMilliseconds;
            double windowStartMs = currentTimeMs;
            //double windowEndMs = currentTimeMs + playbackTimer.Interval.TotalMilliseconds;

            // Reset intensity for all bands for this tick
            double[] newBandIntensities = new double[BAND_BOUNDARIES.Length];

            var activeNotesInWindow = _allProcessedNotes
                .Where(n => n.StartTimeMs <= windowEndMs && n.EndTimeMs >= windowStartMs)
                .ToList();

            foreach (var note in activeNotesInWindow)
            {
                double intensityContribution = (double)note.Note.Velocity / SevenBitNumber.MaxValue; // 0.0 to 1.0

                // Find which band this note belongs to
                for (int i = 0; i < BAND_BOUNDARIES.Length; i++)
                {
                    // Each MIDI note number corresponds to a specific musical pitch and, by extension, a specific fundamental frequency. 
                    // The first band starts at 0.
                    // A note belongs to band 'i' if its NoteNumber is less than or equal to BAND_BOUNDARIES[i].
                    // And if it's not in a lower band (implicitly handled by order).
                    if (note.Note.NoteNumber <= BAND_BOUNDARIES[i])
                    {
                        newBandIntensities[i] += intensityContribution;
                        break; // Note found its band, move to next note
                    }
                }
            }

            // Apply smoothing and decay to each band's intensity
            double decayRate = 0.8; // How much the intensity decays each tick (0.0-1.0, 1.0 means no decay)
            double attackRate = 0.2; // How quickly it rises (0.0-1.0)
            //double maxPossibleIntensityPerBand = 5.0; // Adjust based on your average note density per band

            double canvasHeight = eqCanvas.ActualHeight;

            for (int i = 0; i < BAND_BOUNDARIES.Length; i++)
            {
                // 1. Smooth the current intensity
                _currentBandIntensities[i] = newBandIntensities[i] * attackRate + _currentBandIntensities[i] * (1 - attackRate);
                _currentBandIntensities[i] *= decayRate; // Apply decay

                // 2. Update the peak intensity for normalization
                // If current intensity is higher, update the peak
                _peakBandIntensities[i] = Math.Max(_peakBandIntensities[i], _currentBandIntensities[i]);

                // 3. Apply peak decay
                _peakBandIntensities[i] *= PEAK_DECAY_RATE;

                // 4. Ensure peak doesn't go below a certain threshold to prevent extreme scaling
                _peakBandIntensities[i] = Math.Max(_peakBandIntensities[i], MIN_PEAK_THRESHOLD);

                // 5. Normalize using the *dynamic* peak intensity
                double normalizedIntensity = 0.0;
                if (_peakBandIntensities[i] > 0) // Avoid division by zero
                {
                    normalizedIntensity = Math.Min(1.0, Math.Max(0, _currentBandIntensities[i] / _peakBandIntensities[i]));
                }


                // Update the visual bar
                Rectangle currentBar = _eqBars[i];
                if (currentBar != null)
                {
                    double barHeight = normalizedIntensity * canvasHeight;
                    currentBar.Height = barHeight;
                    double barLength = canvasHeight - barHeight; // Make it rise from the bottom
                    Canvas.SetTop(currentBar, barLength);
                    currentBar.Visibility = Visibility.Visible; // Ensure it's visible if it has height
                }
            }
        }



    }
}
