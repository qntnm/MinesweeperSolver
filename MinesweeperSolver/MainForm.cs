﻿using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace MinesweeperSolver {

    public partial class MainForm : Form {
        private readonly string _screenshotLocation = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MinesweeperFails");

        public MainForm() {
            InitializeComponent();

            if (!Directory.Exists(_screenshotLocation))
                Directory.CreateDirectory(_screenshotLocation);
        }

        private void StartSolve(object sender, EventArgs e) {
            MineSolver.Running = !MineSolver.Running;
            if (!MineSolver.Running)
                btnSolve.Enabled = false; // Let the thread re-enable this when it's stopped
            else
                new Thread(SolveSweeper).Start();
        }

        // Thread functions

        /// <summary>
        ///     Finds the Minesweeper window, and if found, starts to solve
        ///     It does this by taking a screenshot, analyzing the numbers based on colour, and performing the best action
        ///     Updates the UI with the screenshot which shows exactly what's being analyzed
        ///     Saves a screenshot of the current board when it fails, then restarts
        /// </summary>
        private void SolveSweeper() {
            Invoke((MethodInvoker) delegate {
                btnSolve.Text = @"Stop";
                strWaiting.Visible = true;
                strStatus.Text = @"Solving...";
            });

            if (!MineSolver.FindSweeperWindow()) {
                Invoke((MethodInvoker) delegate {
                    btnSolve.Text = @"Solve";
                    strWaiting.Visible = false;
                    strStatus.Text = @"Couldn't find game!";
                });
                MineSolver.Running = false;
                return;
            }

            var board = new Board(ScreenHelper.GetMinesweeperScreenshot());

            // Timer for checking if the Minesweeper window is still there
            var tmr = new Stopwatch();
            tmr.Start();

            var willDoubleScreenshot = false; // whether or not the bot screenshotted - to stop multiple screenshots without limiting performance

            Invoke((MethodInvoker) delegate {
                strWaiting.Style = ProgressBarStyle.Continuous;
                strWaiting.Value = 0;
                strWaiting.Maximum = 100;
            });

            // Stop this loop on button click (Running), form exit or completion
            while (MineSolver.Running && !IsDisposed) {
                // Grab new screenshot
                var img = ScreenHelper.GetMinesweeperScreenshot();
                board.Update(img, willDoubleScreenshot);

                // Fail
                if (board.IsFailed) {
                    if (willDoubleScreenshot)
                        continue;
                    willDoubleScreenshot = true;
                    Console.WriteLine(@"Failed!");
                    MineSolver.ClickSweeperRestart();
                    var boardSizeDirectory = Path.Combine(_screenshotLocation, $"{board.Columns}x{board.Rows}");
                    if (!Directory.Exists(boardSizeDirectory))
                        Directory.CreateDirectory(boardSizeDirectory);
                    var filename = Path.Combine(boardSizeDirectory, $"{board.Score}-fail-{(int) DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds}.png");
                    Console.WriteLine($" -> Saved screenshot to {filename.Replace(AppDomain.CurrentDomain.BaseDirectory, "")}");
                    img.Save(filename);
                    continue;
                }

                // Finished
                if (board.IsComplete) {
                    if (willDoubleScreenshot)
                        continue;
                    willDoubleScreenshot = true;
                    Console.WriteLine(@"Won!");
                    var successDirectory = Path.Combine(_screenshotLocation, $"Wins");
                    if (!Directory.Exists(successDirectory))
                        Directory.CreateDirectory(successDirectory);
                    var filename = Path.Combine(successDirectory, $"win-{(int)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds}.png");
                    Console.WriteLine($" -> Saved screenshot to {filename.Replace(AppDomain.CurrentDomain.BaseDirectory, "")}");
                    img.Save(filename);
                    break;
                }
                willDoubleScreenshot = false;

                // New board
                if (board.IsNew) {
                    Console.WriteLine($"============================\nNew game!");
                    MineSolver.ClickSweeperSquare(0, 0, ref board);
                    continue;
                }

                // Clicks
                MineSolver.DoBestSweeperActions(board);

                // Window check, every second because checking too often massively hurts performance
                if (tmr.Elapsed.TotalMilliseconds >= 1000) {
                    tmr.Restart();
                    if (!MineSolver.FindSweeperWindow(true))
                        break;
                }

                // Update picturebox - might change to an image based on what the bot sees for more insight
                imgGame.Image = board.GetVisualization(); //img;
                Invoke((MethodInvoker) delegate { strWaiting.Value = board.Score; });
            }

            // Done!
            MineSolver.Running = false;
            if (!IsDisposed) {
                Invoke((MethodInvoker) delegate {
                    btnSolve.Enabled = true;
                    strWaiting.Visible = false;
                    btnSolve.Text = @"Solve";
                    strStatus.Text = @"Finished " + (board.IsComplete ? " successfully!" : " with a loss :(");
                });
            }
        }
    }

}