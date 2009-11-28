﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using Daiz.Library;
using Daiz.NES.Reuben.ProjectManagement;
using Dotnetrix.Controls;

namespace Daiz.NES.Reuben
{
    public partial class LevelEditor : Form
    {
        PatternTable CurrentTable;
        PaletteInfo CurrentPalette;
        int LeftMouseTile = 0;
        int RightMouseTile = 0;
        private MouseMode MouseMode = MouseMode.RightClickSelection;
        public Level CurrentLevel { get; private set; }

        public LevelEditor()
        {
            InitializeComponent();

            LvlView.DelayDrawing = true;
            UndoBuffer = new List<IUndoableAction>();
            RedoBuffer = new List<IUndoableAction>();
            CmbLayouts.DisplayMember = CmbTypes.DisplayMember = CmbPalettes.DisplayMember = CmbGraphics.DisplayMember = "Name";
            foreach (var g in ProjectController.GraphicsManager.GraphicsInfo)
            {
                CmbGraphics.Items.Add(g);
            }

            foreach (var p in ProjectController.PaletteManager.Palettes)
            {
                CmbPalettes.Items.Add(p);
            }

            foreach (var t in ProjectController.LevelManager.LevelTypes)
            {
                if(t.InGameID != 0)
                    CmbTypes.Items.Add(t);
            }

            for (int i = 1; i < 16; i++)
            {
                CmbLength.Items.Add(i);
            }

            foreach (var l in ProjectController.LayoutManager.BlockLayouts)
            {
                CmbLayouts.Items.Add(l);
            }

            CmbLayouts.SelectedIndex = 0;
            CurrentTable = new PatternTable();
            LvlView.CurrentTable = CurrentTable;

            BlsSelector.CurrentTable = CurrentTable;
            BlsSelector.BlockLayout = ProjectController.LayoutManager.BlockLayouts[0];
            BlsSelector.SpecialTable = LvlView.SpecialTable = ProjectController.SpecialManager.SpecialTable;
            BlsSelector.SpecialPalette = LvlView.SpecialPalette = ProjectController.SpecialManager.SpecialPalette;
            BlsSelector.SelectionChanged += new EventHandler<TEventArgs<MouseButtons>>(BlsSelector_SelectionChanged);

            BlvRight.CurrentTable = BlvLeft.CurrentTable = CurrentTable;

            ProjectController.PaletteManager.PaletteAdded += new EventHandler<TEventArgs<PaletteInfo>>(PaletteManager_PaletteAdded);
            ProjectController.PaletteManager.PaletteRemoved += new EventHandler<TEventArgs<PaletteInfo>>(PaletteManager_PaletteRemoved);
            ProjectController.PaletteManager.PalettesSaved += new EventHandler(PaletteManager_PalettesSaved);
            ProjectController.BlockManager.DefinitionsSaved += new EventHandler(BlockManager_DefinitionsSaved);
            ProjectController.LayoutManager.LayoutAdded += new EventHandler<TEventArgs<BlockLayout>>(LayoutManager_LayoutAdded);
            ProjectController.GraphicsManager.GraphicsUpdated += new EventHandler(GraphicsManager_GraphicsUpdated);
            ProjectController.LayoutManager.LayoutRemoved += new EventHandler<TEventArgs<BlockLayout>>(LayoutManager_LayoutRemoved);
            LvlView.Zoom = 1;
            PnlVerticalGuide.Guide1Changed += new EventHandler(PnlVerticalGuide_Guide1Changed);
            PnlVerticalGuide.Guide2Changed += new EventHandler(PnlVerticalGuide_Guide2Changed);
            PnlHorizontalGuide.Guide1Changed += new EventHandler(PnlHorizontalGuide_Guide1Changed);
            PnlHorizontalGuide.Guide2Changed += new EventHandler(PnlHorizontalGuide_Guide2Changed);
            ReubenController.GraphicsReloaded += new EventHandler(ReubenController_GraphicsReloaded);
            ReubenController.LevelReloaded += new EventHandler<TEventArgs<Level>>(ReubenController_LevelReloaded);
            LoadSpriteSelector();
            LvlView.HorizontalGuide1 = PnlHorizontalGuide.Guide1;
            LvlView.HorizontalGuide2 = PnlHorizontalGuide.Guide2;
            LvlView.VerticalGuide1 = PnlVerticalGuide.Guide1;
            LvlView.VerticalGuide2 = PnlVerticalGuide.Guide2;
            LvlView.DelayDrawing = false;
            LvlView.FullUpdate();
        }

        void BlsSelector_SelectionChanged(object sender, TEventArgs<MouseButtons> e)
        {
            if (MouseMode == MouseMode.RightClickSelection)
            {
                LblSelected.Text = "Drawing With: " + LeftMouseTile.ToHexString();
                LeftMouseTile = BlsSelector.SelectedTileIndex;
                BlvLeft.PaletteIndex = (LeftMouseTile & 0xC0) >> 6;
                BlvLeft.CurrentBlock = BlsSelector.SelectedBlock;
            }
            else
            {
                if (e != null)
                {
                    if (e.Data == MouseButtons.Left)
                    {
                        LeftMouseTile = BlsSelector.SelectedTileIndex;
                        BlvLeft.PaletteIndex = (LeftMouseTile & 0xC0) >> 6;
                        BlvLeft.CurrentBlock = BlsSelector.SelectedBlock;
                    }
                    else
                    {
                        RightMouseTile = BlsSelector.SelectedTileIndex;
                        BlvRight.PaletteIndex = (RightMouseTile & 0xC0) >> 6;
                        BlvRight.CurrentBlock = BlsSelector.SelectedBlock;
                    }
                }

                LblSelected.Text = "Left: " + LeftMouseTile + " - Right: " + RightMouseTile;
            }
        }

        void ReubenController_LevelReloaded(object sender, TEventArgs<Level> e)
        {
            if (CurrentLevel == e.Data)
            {
                GetLevelInfo(e.Data);
            }
        }

        void ReubenController_GraphicsReloaded(object sender, EventArgs e)
        {
            CurrentTable.SetGraphicsbank(2, ProjectController.GraphicsManager.GraphicsBanks[CurrentLevel.AnimationBank]);
            CurrentTable.SetGraphicsbank(3, ProjectController.GraphicsManager.GraphicsBanks[CurrentLevel.AnimationBank + 1]);
            UpdateGraphics();
        }

        void LayoutManager_LayoutRemoved(object sender, TEventArgs<BlockLayout> e)
        {
            CmbLayouts.Items.Add(e.Data);
        }

        void LayoutManager_LayoutAdded(object sender, TEventArgs<BlockLayout> e)
        {
            if (CmbLayouts.SelectedItem == e.Data)
            {
                CmbLayouts.SelectedIndex--;
            }

            CmbLayouts.Items.Remove(e.Data);
        }

        void PaletteManager_PalettesSaved(object sender, EventArgs e)
        {
            UpdateGraphics();
        }

        public void EditLevel(Level l)
        {
            GetLevelInfo(l);

            if (!ProjectController.SettingsManager.HasLevelSettings(l.Guid))
            {
                ProjectController.SettingsManager.AddLevelSettings(l.Guid);
            }

            TsbGrid.Checked = ProjectController.SettingsManager.GetLevelSetting<bool>(l.Guid, "ShowGrid");
            TsbTileSpecials.Checked = ProjectController.SettingsManager.GetLevelSetting<bool>(l.Guid, "SpecialTiles");
            TsbSriteSpecials.Checked = ProjectController.SettingsManager.GetLevelSetting<bool>(l.Guid, "SpecialSprites");
            TsbProperties.Checked = ProjectController.SettingsManager.GetLevelSetting<bool>(l.Guid, "BlockProperties");
            TsbStartPoint.Checked = ProjectController.SettingsManager.GetLevelSetting<bool>(l.Guid, "ShowStart");
            TsbZoom.Checked = ProjectController.SettingsManager.GetLevelSetting<bool>(l.Guid, "Zoom");

            switch (ProjectController.SettingsManager.GetLevelSetting<string>(l.Guid, "Draw"))
            {
                case "Pencil":
                    TsbPencil.Checked = true;
                    DrawMode = DrawMode.Pencil;
                    break;

                case "Rectangle":
                    TsbRectangle.Checked = true;
                    DrawMode = DrawMode.Rectangle;
                    break;

                case "Outline":
                    TsbOutline.Checked = true;
                    DrawMode = DrawMode.Outline;
                    break;

                case "Line":
                    TsbLine.Checked = true;
                    DrawMode = DrawMode.Line;
                    break;

                case "Fill":
                    TsbBucket.Checked = true;
                    DrawMode = DrawMode.Fill;
                    break;

                case "Scatter":
                    TsbScatter.Checked = true;
                    DrawMode = DrawMode.Scatter;
                    break;
            }

            CmbLayouts.SelectedIndex = ProjectController.SettingsManager.GetLevelSetting<int>(l.Guid, "Layout");
            PnlHorizontalGuide.GuideColor = ProjectController.SettingsManager.GetLevelSetting<Color>(l.Guid, "HGuideColor");
            PnlVerticalGuide.GuideColor = ProjectController.SettingsManager.GetLevelSetting<Color>(l.Guid, "VGuideColor");

            this.Text = ProjectController.LevelManager.GetLevelInfo(l.Guid).Name;
            this.WindowState = FormWindowState.Maximized;
            this.Show();
            SetMiscText(0);
            BtnShowHideInfo_Click(null, null);
            LvlView.FullUpdate();
        }

        private void GetLevelInfo(Level l)
        {
            LvlView.CurrentLevel = l;
            CurrentLevel = l;
            NumTime.Value = l.Time;
            NumBackground.Value = l.ClearValue;
            CurrentTable.SetGraphicsbank(2, ProjectController.GraphicsManager.GraphicsBanks[CurrentLevel.AnimationBank]);
            CurrentTable.SetGraphicsbank(3, ProjectController.GraphicsManager.GraphicsBanks[CurrentLevel.AnimationBank + 1]);
            CmbGraphics.SelectedIndex = l.GraphicsBank;
            CmbPalettes.SelectedIndex = l.Palette;
            CmbTypes.SelectedIndex = l.Type - 1;
            CmbActions.SelectedIndex = l.StartAction;
            CmbScroll.SelectedIndex = l.ScrollType;
            CmbMusic.SelectedIndex = l.Music;
            if (l.LevelLayout == LevelLayout.Vertical)
            {
                CmbScroll.SelectedIndex = 1;
                CmbScroll.Text = "Vertical Scrolling";
                CmbScroll.Enabled = false;
            }
            CmbLength.SelectedItem = l.Length;
            PntEditor.CurrentPointer = null;
            BtnAddPointer.Enabled = CurrentLevel.Pointers.Count <= 4;
            BtnDeletePointer.Enabled = false;
            CurrentLevel.TilesModified += new EventHandler<TEventArgs<TileInformation>>(CurrentLevel_TilesModified);
            GetCoinTotals();
            UpdateCoinTotalText();
            NumSpecials.Value = (decimal) ProjectController.SettingsManager.GetLevelSetting<double>(CurrentLevel.Guid, "TransSpecials");
            NumProperties.Value = (decimal) ProjectController.SettingsManager.GetLevelSetting<double>(CurrentLevel.Guid, "TransProps");
        }

        #region guide events
        private void PnlHorizontalGuide_Guide2Changed(object sender, EventArgs e)
        {
            LvlView.UpdateGuide(Orientation.Horizontal, 2);
        }

        private void PnlHorizontalGuide_Guide1Changed(object sender, EventArgs e)
        {
            LvlView.UpdateGuide(Orientation.Horizontal, 1);
        }

        private void PnlVerticalGuide_Guide2Changed(object sender, EventArgs e)
        {
            LvlView.UpdateGuide(Orientation.Vertical, 2);
        }

        private void PnlVerticalGuide_Guide1Changed(object sender, EventArgs e)
        {
            LvlView.UpdateGuide(Orientation.Vertical, 1);
        }

        private void freeGuideToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PnlVerticalGuide.GuideSnapMode = GuideMode.Free;
            freeGuideToolStripMenuItem.Checked = true;
            snapToEnemyBounceHeightToolStripMenuItem.Checked =
            showScreenHeightToolStripMenuItem.Checked =
            snapToJumpHeightToolStripMenuItem.Checked =
            snapToRunningJumpHeightToolStripMenuItem.Checked =
            snapToFullPMeterJumpHeightToolStripMenuItem.Checked = false;
        }

        private void snapToJumpHeightToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PnlVerticalGuide.GuideSnapMode = GuideMode.JumpHeight1;
            snapToJumpHeightToolStripMenuItem.Checked = true;
            snapToEnemyBounceHeightToolStripMenuItem.Checked =
            showScreenHeightToolStripMenuItem.Checked =
            freeGuideToolStripMenuItem.Checked =
            snapToRunningJumpHeightToolStripMenuItem.Checked =
            snapToFullPMeterJumpHeightToolStripMenuItem.Checked = false;
        }

        private void showScreenHeightToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PnlVerticalGuide.GuideSnapMode = GuideMode.Screen;
            showScreenHeightToolStripMenuItem.Checked = true;
            snapToEnemyBounceHeightToolStripMenuItem.Checked =
            snapToJumpHeightToolStripMenuItem.Checked =
            freeGuideToolStripMenuItem.Checked =
            snapToRunningJumpHeightToolStripMenuItem.Checked =
            snapToFullPMeterJumpHeightToolStripMenuItem.Checked = false;

        }

        private void snapToRunningJumpHeightToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PnlVerticalGuide.GuideSnapMode = GuideMode.JumpHeight2;
            snapToRunningJumpHeightToolStripMenuItem.Checked = true;
            snapToEnemyBounceHeightToolStripMenuItem.Checked =
            showScreenHeightToolStripMenuItem.Checked =
            snapToJumpHeightToolStripMenuItem.Checked =
            freeGuideToolStripMenuItem.Checked =
            snapToFullPMeterJumpHeightToolStripMenuItem.Checked = false;
        }

        private void snapToFullPMeterJumpHeightToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PnlVerticalGuide.GuideSnapMode = GuideMode.JumpHeight3;
            snapToFullPMeterJumpHeightToolStripMenuItem.Checked = true;
            snapToEnemyBounceHeightToolStripMenuItem.Checked =
            snapToRunningJumpHeightToolStripMenuItem.Checked =
            showScreenHeightToolStripMenuItem.Checked =
            snapToJumpHeightToolStripMenuItem.Checked =
            freeGuideToolStripMenuItem.Checked = false;
        }


        private void snapToEnemyBounceHeightToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PnlVerticalGuide.GuideSnapMode = GuideMode.JumpHeight4;
            snapToEnemyBounceHeightToolStripMenuItem.Checked = true;
            snapToFullPMeterJumpHeightToolStripMenuItem.Checked =
            snapToRunningJumpHeightToolStripMenuItem.Checked =
            showScreenHeightToolStripMenuItem.Checked =
            snapToJumpHeightToolStripMenuItem.Checked =
            freeGuideToolStripMenuItem.Checked = false;
        }

        private void hideGuidesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PnlVerticalGuide.Guide1.Visible = PnlVerticalGuide.Guide2.Visible = false;
            LvlView.UpdateGuide(Orientation.Vertical, 1);
            LvlView.UpdateGuide(Orientation.Vertical, 2);
        }

        private void snapToJumpLengthToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PnlHorizontalGuide.GuideSnapMode = GuideMode.JumpLength1;
            snapToScreenLengthToolStripMenuItem.Checked =
            snapToJumpLengthToolStripMenuItem.Checked = true;
            freeGuide2.Checked =
            snapToWalkingJumpLengthToolStripMenuItem.Checked =
            snapToRunningJumpHeightToolStripMenuItem.Checked =
            snapToFullMeterJumpLengthToolStripMenuItem.Checked = false;
        }

        private void snapToWalkingJumpLengthToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PnlHorizontalGuide.GuideSnapMode = GuideMode.JumpLength2;
            snapToWalkingJumpLengthToolStripMenuItem.Checked = true;
            snapToScreenLengthToolStripMenuItem.Checked =
            freeGuide2.Checked =
            snapToJumpLengthToolStripMenuItem.Checked =
            snapToRunningJumpHeightToolStripMenuItem.Checked =
            snapToFullMeterJumpLengthToolStripMenuItem.Checked = false;
        }

        private void snapToRunningJumpLengthToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PnlHorizontalGuide.GuideSnapMode = GuideMode.JumpLength3;
            snapToRunningJumpLengthToolStripMenuItem.Checked = true;
            snapToScreenLengthToolStripMenuItem.Checked =
            snapToWalkingJumpLengthToolStripMenuItem.Checked =
            freeGuide2.Checked =
            snapToJumpLengthToolStripMenuItem.Checked =
            snapToFullMeterJumpLengthToolStripMenuItem.Checked = false;
        }

        private void snapToFullMeterJumpLengthToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PnlHorizontalGuide.GuideSnapMode = GuideMode.JumpLength4;
            snapToFullMeterJumpLengthToolStripMenuItem.Checked = true;
            snapToScreenLengthToolStripMenuItem.Checked =
            snapToRunningJumpHeightToolStripMenuItem.Checked =
            snapToWalkingJumpLengthToolStripMenuItem.Checked =
            freeGuide2.Checked =
            snapToJumpLengthToolStripMenuItem.Checked = false;
        }

        private void snapToScreenLengthToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PnlHorizontalGuide.GuideSnapMode = GuideMode.Screen;
            snapToScreenLengthToolStripMenuItem.Checked = true;
            snapToFullMeterJumpLengthToolStripMenuItem.Checked =
            snapToRunningJumpHeightToolStripMenuItem.Checked =
            snapToWalkingJumpLengthToolStripMenuItem.Checked =
            freeGuide2.Checked =
            snapToJumpLengthToolStripMenuItem.Checked = false;
        }
        #endregion

        #region coin information gathering
        void CurrentLevel_TilesModified(object sender, TEventArgs<TileInformation> e)
        {
            switch (e.Data.Previous)
            {
                case 0x05:
                    pSwitchCoins--;
                    break;

                case 0x40:
                    coins--;
                    break;

                case 0x63:
                case 0x65:
                    coinblocks--;
                    break;

                case 0x67:
                    pSwitchCoins--;
                    break;

                case 0x44:
                    invisibleCoins--;
                    break;

                case 0x6D:
                    multicoins--;
                    break;
            }

            switch (e.Data.Current)
            {
                case 0x05:
                    pSwitchCoins++;
                    break;

                case 0x40:
                    coins++;
                    break;

                case 0x63:
                case 0x65:
                    coinblocks++;
                    break;

                case 0x67:
                    pSwitchCoins++;
                    break;

                case 0x6D:
                    multicoins++;
                    break;

                case 0x44:
                    invisibleCoins++;
                    break;
            }
        }

        //Coin info
        int coins = 0;
        int coinblocks = 0;
        int multicoins = 0;
        int pSwitchCoins = 0;
        int invisibleCoins = 0;

        private void GetCoinTotals()
        {
            pSwitchCoins = coinblocks = multicoins = coinblocks = invisibleCoins = 0;

            for (int i = 0; i < CurrentLevel.Width; i++)
            {
                for (int j = 0; j < CurrentLevel.Height; j++)
                {
                    switch (CurrentLevel.LevelData[i, j])
                    {
                        case 0x05:
                            pSwitchCoins++;
                            break;

                        case 0x40:
                            coins++;
                            break;

                        case 0x63:
                        case 0x65:
                            coinblocks++;
                            break;

                        case 0x67:
                            pSwitchCoins++;
                            break;

                        case 0x44:
                            invisibleCoins++;
                            break;

                        case 0x6D:
                            multicoins++;
                            break;
                    }
                }
            }
        }

        void UpdateCoinTotalText()
        {
            LblInvisibleCoins.Text = "Invisible Coin Blocks: " + invisibleCoins;
            LblCoins.Text = "Coins: " + coins;
            LblMultiCoins.Text = "Multi Coin Blocks: " + multicoins;
            LblCoinBlocks.Text = "Coin Blocks: " + coinblocks;
            LblPSwitchCoins.Text = "P-Switch Coins: " + pSwitchCoins;
            LblTotalCoins.Text = "Total Possible Coins: " + (coins + coinblocks + pSwitchCoins + invisibleCoins + (multicoins * 10)).ToString();
        }
        #endregion

        #region palette functions
        void PaletteManager_PaletteAdded(object sender, TEventArgs<PaletteInfo> e)
        {
            CmbPalettes.Items.Add(e.Data);
        }

        void PaletteManager_PaletteRemoved(object sender, TEventArgs<PaletteInfo> e)
        {
            int index = CmbPalettes.SelectedIndex;
            CmbPalettes.Items.Remove(sender);
            if (CmbPalettes.Items.Count - 1 < index)
            {
                CmbPalettes.SelectedIndex = CmbPalettes.Items.Count - 1;
            }
            else
            {
                CmbPalettes.SelectedIndex = index;
            }
        }
        #endregion

        #region graphics functions
        void GraphicsManager_GraphicsUpdated(object sender, EventArgs e)
        {
            UpdateGraphics();
        }

        private void UpdateGraphics()
        {
            CurrentTable = new PatternTable();
            CurrentTable.SetGraphicsbank(0, ProjectController.GraphicsManager.GraphicsBanks[CmbGraphics.SelectedIndex]);
            CurrentTable.SetGraphicsbank(1, ProjectController.GraphicsManager.GraphicsBanks[CmbGraphics.SelectedIndex + 1]);
            CurrentTable.SetGraphicsbank(2, ProjectController.GraphicsManager.GraphicsBanks[CurrentLevel.AnimationBank]);
            CurrentTable.SetGraphicsbank(3, ProjectController.GraphicsManager.GraphicsBanks[CurrentLevel.AnimationBank + 1]);
            BlvRight.CurrentPalette = BlvLeft.CurrentPalette = BlsSelector.CurrentPalette = LvlView.CurrentPalette = CurrentPalette;
            BlsSelector.CurrentDefiniton = LvlView.CurrentDefiniton = LvlView.CurrentDefiniton;
            BlvRight.CurrentBlock = BlvRight.CurrentBlock;
            BlvLeft.CurrentBlock = BlvLeft.CurrentBlock;
        }
        #endregion

        #region mouse functions
        private bool useTransparentTile;
        private int StartX, StartY, FromX, FromY, ToX, ToY;
        private bool ContinueDragging;
        private DrawMode PreviousMode;

        private void LvlView_MouseDown(object sender, MouseEventArgs e)
        {
            int x = (e.X / 16) / LvlView.Zoom;
            int y = (e.Y / 16) / LvlView.Zoom;
            PnlView.Focus();

            if (x < 0 || x >= CurrentLevel.Width || y < 0 || y >= CurrentLevel.Height) return;

            if (_SelectingStartPositionMode)
            {
                int oldX = CurrentLevel.XStart;
                int oldY = CurrentLevel.YStart;
                CurrentLevel.XStart = x;
                CurrentLevel.YStart = y;
                if (TsbStartPoint.Checked)
                {
                    LvlView.UpdatePoint(x, y);
                    LvlView.UpdatePoint(oldX, oldY);
                }
                _SelectingStartPositionMode = false;
                PnlDrawing.Enabled = TabLevelInfo.Enabled = true;
                SetMiscText(PreviousTextIndex);
            }

            else if (EditMode == EditMode.Tiles)
            {
                if (ModifierKeys == Keys.Shift)
                {
                    BlsSelector.SelectedTileIndex = CurrentLevel.LevelData[x, y];
                    BlsSelector_SelectionChanged(this, new TEventArgs<MouseButtons>(e.Button));
                }
                else
                {
                    LvlView.ClearSelection();
                    if (DrawMode == DrawMode.Selection)
                    {
                        DrawMode = PreviousMode;
                    }

                    if (e.Button == MouseButtons.Right && MouseMode == MouseMode.RightClickSelection)
                    {
                        PreviousMode = DrawMode;
                        DrawMode = DrawMode.Selection;
                    }

                    switch (DrawMode)
                    {
                        case DrawMode.Pencil:
                            CurrentMultiTile = new MultiTileAction();
                            CurrentMultiTile.AddTileChange(x, y, CurrentLevel.LevelData[x, y]);
                            CurrentLevel.SetTile(x, y, (byte)(DrawingTile));
                            ContinueDrawing = true;    
                            break;

                        case DrawMode.Outline:
                        case DrawMode.Rectangle:
                        case DrawMode.Scatter:
                        case DrawMode.Selection:
                            StartX = x;
                            StartY = y;
                            ContinueDrawing = true;
                            LvlView.SelectionRectangle = new Rectangle(StartX, StartY, 1, 1);
                            break;

                        case DrawMode.Line:
                            StartX = x;
                            StartY = y;
                            ContinueDrawing = true;
                            LvlView.SelectionLine = new Line(StartX, StartY, StartX, StartY);
                            break;

                        case DrawMode.Fill:
                            Point start = new Point(x, y);
                            Stack<Point> stack = new Stack<Point>();
                            stack.Push(start);
                            int checkValue = CurrentLevel.LevelData[x, y];
                            if (checkValue == DrawingTile) return;

                            CurrentMultiTile = new MultiTileAction();
                            while (stack.Count > 0)
                            {
                                Point p = stack.Pop();
                                int lowestX, highestX; ;
                                int lowestY, highestY;
                                lowestX = highestX = x;
                                lowestY = highestY = y;
                                int i = p.X;
                                int j = p.Y;
                                if (CurrentLevel.LevelLayout == LevelLayout.Horizontal)
                                {
                                    if (j < 0 || j >= CurrentLevel.Height || i < 0 || i >= CurrentLevel.Length * 16)
                                    {
                                        continue;
                                    }
                                }
                                else
                                {
                                    if (j < 0 || j >= (CurrentLevel.Length * 15) - 4 || i < 0 || i >= CurrentLevel.Width)
                                    {
                                        continue;
                                    }
                                }

                                LvlView.DelayDrawing = true;
                                if (checkValue == CurrentLevel.LevelData[i, j])
                                {
                                    CurrentMultiTile.AddTileChange(i, j, CurrentLevel.LevelData[i, j]);
                                    CurrentLevel.SetTile(i, j, (byte)DrawingTile);
                                    if (i < lowestX) lowestX = i;
                                    if (i > highestX) highestX = i;
                                    if (j < lowestY) lowestY = j;
                                    if (j > highestY) highestY = j;

                                    stack.Push(new Point(i + 1, j));
                                    stack.Push(new Point(i - 1, j));
                                    stack.Push(new Point(i, j + 1));
                                    stack.Push(new Point(i, j - 1));
                                }
                                UndoBuffer.Add(CurrentMultiTile);
                                LvlView.DelayDrawing = false;
                                LvlView.UpdateArea(new Rectangle(lowestX, lowestY, highestX - lowestX + 1, highestY - lowestY + 1));
                            }

                            break;
                    }
                }
            }
            else if (EditMode == EditMode.Sprites)
            {
                CurrentSprite = SelectSprite(x, y);
                if (CurrentSprite != null && MouseButtons == MouseButtons.Left)
                {
                    LvlView.SelectionRectangle = new Rectangle(CurrentSprite.X, CurrentSprite.Y, CurrentSprite.Width, CurrentSprite.Height);
                    ContinueDragging = true;
                    LblSprite.Text = "Current Sprite: " + CurrentSprite.InGameID.ToHexString() + " - " + CurrentSprite.Name;
                }
                else if (CurrentSprite != null && MouseButtons == MouseButtons.Right && CurrentSelectorSprite != null)
                {
                    CurrentSprite.InGameID = CurrentSelectorSprite.InGameID;
                    SpriteDefinition sp = ProjectController.SpriteManager.GetDefinition(CurrentSprite.InGameID);
                    int xDiff = x - CurrentSprite.X;
                    int yDiff = y - CurrentSprite.Y;
                    int rectX = xDiff >= 0 ? (CurrentSprite.X * 16) + sp.MaxLeftX : (x * 16) + sp.MaxLeftX;
                    int rectY = yDiff >= 0 ? (CurrentSprite.Y * 16) + sp.MaxTopY : (y * 16) + sp.MaxTopY;
                    int width = xDiff >= 0 ? ((x * 16) + sp.MaxRightX) - ((CurrentSprite.X * 16) + sp.MaxLeftX) : ((CurrentSprite.X * 16) + sp.MaxRightX) - ((x * 16) + sp.MaxLeftX);
                    int height = yDiff >= 0 ? ((y * 16) + sp.MaxBottomY) - ((CurrentSprite.Y * 16) + sp.MaxTopY) : ((CurrentSprite.Y * 16) + sp.MaxBottomY) - ((y * 16) + sp.MaxTopY);
                    Rectangle r = new Rectangle(rectX, rectY, width, height);
                    CurrentSprite.X = x;
                    CurrentSprite.Y = y;
                    LvlView.DelayDrawing = true;
                    LvlView.SelectionRectangle = new Rectangle(CurrentSprite.X, CurrentSprite.Y, CurrentSprite.Width, CurrentSprite.Height);
                    LvlView.DelayDrawing = false;
                    LvlView.UpdateSprites(r);
                }
                else if (CurrentSelectorSprite != null && MouseButtons == MouseButtons.Right)
                {
                    Sprite newSprite = new Sprite() { X = x, Y = y, InGameID = CurrentSelectorSprite.InGameID };
                    CurrentLevel.AddSprite(newSprite);
                    CurrentSprite = newSprite;
                    LvlView.SelectionRectangle = new Rectangle(CurrentSprite.X, CurrentSprite.Y, CurrentSprite.Width, CurrentSprite.Height);
                    ContinueDragging = true;
                    LblSprite.Text = "Current Sprite: " + CurrentSprite.InGameID.ToHexString() + " - " + CurrentSprite.Name;
                }

                else
                {
                    LvlView.ClearSelection();
                    ContinueDragging = false;
                    LblSprite.Text = "None";
                }
            }
            else if (EditMode == EditMode.Pointers)
            {
                LevelPointer p = CurrentLevel.Pointers.Find(pt => (pt.XEnter == x || pt.XEnter + 1 == x) && (pt.YEnter == y || pt.YEnter + 1 == y));
                PntEditor.CurrentPointer = p;
                CurrentPointer = p;
                if (p != null)
                {
                    LvlView.SelectionRectangle = new Rectangle(p.XEnter, p.YEnter, 2, 2);
                    ContinueDragging = true;
                    BtnDeletePointer.Enabled = true;
                }
                else
                {
                    BtnDeletePointer.Enabled = false;
                    LvlView.ClearSelection();
                }
            }
        }

        private int PreviousMouseX, PreviousMouseY;
        private void LvlView_MouseMove(object sender, MouseEventArgs e)
        {
            int x = (e.X / (16 * LvlView.Zoom));
            int y = (e.Y / (16 * LvlView.Zoom));

            if (x < 0 || x >= CurrentLevel.Width || y < 0 || y >= CurrentLevel.Height) return;

            if (PreviousMouseX == x && PreviousMouseY == y) return;
            PreviousMouseX = x;
            PreviousMouseY = y;

            int XDiff = x - StartX;
            int YDiff = y - StartY;

            LblPositition.Text = "X: " + x.ToHexString() + " Y: " + y.ToHexString();

            if (EditMode == EditMode.Tiles)
            {
                LevelToolTip.SetToolTip(LvlView, ProjectController.BlockManager.GetBlockString(CurrentLevel.Type, CurrentLevel.LevelData[x, y]) + "\n" + ProjectController.SpecialManager.GetProperty(CurrentLevel.Type, CurrentLevel.LevelData[x, y]) + "\n(" + CurrentLevel.LevelData[x, y].ToHexString() + ")");
                if (ContinueDrawing && (MouseButtons == MouseButtons.Left || MouseButtons == MouseButtons.Middle || MouseButtons == MouseButtons.Right))
                {
                    switch (DrawMode)
                    {
                        case DrawMode.Pencil:
                            CurrentMultiTile.AddTileChange(x, y, CurrentLevel.LevelData[x, y]);
                            CurrentLevel.SetTile(x, y, (byte)DrawingTile);
                            break;

                        case DrawMode.Outline:
                        case DrawMode.Rectangle:
                        case DrawMode.Selection:
                        case DrawMode.Scatter:
                            if (StartX == x && StartY == y) return;
                            if (x > StartX)
                            {
                                FromX = StartX;
                                ToX = x;
                            }
                            else
                            {
                                FromX = x;
                                ToX = StartX;
                            }

                            if (y > StartY)
                            {
                                FromY = StartY;
                                ToY = y;
                            }
                            else
                            {
                                FromY = y;
                                ToY = StartY;
                            }


                            LvlView.SelectionRectangle = new Rectangle(FromX, FromY, (ToX - FromX) + 1, (ToY - FromY) + 1);
                            break;

                        case DrawMode.Line:
                            if (y > StartY)
                            {
                                if (x > StartX)
                                {
                                    LvlView.SelectionLine = new Line(StartX, StartY, x, StartY + (x - StartX));
                                }
                                else
                                {
                                    LvlView.SelectionLine = new Line(StartX, StartY, x, StartY - (x - StartX));
                                }
                            }
                            else
                            {
                                if (x > StartX)
                                {
                                    LvlView.SelectionLine = new Line(StartX, StartY, x, StartY - (x - StartX));
                                }
                                else
                                {
                                    LvlView.SelectionLine = new Line(StartX, StartY, x, StartY + (x - StartX));
                                }
                            }
                            break;
                    }
                }
            }
            else if (EditMode == EditMode.Sprites)
            {
                Sprite s = SelectSprite(x, y);

                if (s != null)
                {
                    LevelToolTip.SetToolTip(LvlView, s.Name + "\n(" + s.InGameID.ToHexString() + ")");
                }
                else
                {
                    LevelToolTip.SetToolTip(LvlView, null);
                }

                if (ContinueDragging && MouseButtons == MouseButtons.Left && CurrentSprite != null)
                {
                    if (x != CurrentSprite.X || y != CurrentSprite.Y)
                    {
                        SpriteDefinition sp = ProjectController.SpriteManager.GetDefinition(CurrentSprite.InGameID);
                        int xDiff = x - CurrentSprite.X;
                        int yDiff = y - CurrentSprite.Y;
                        int rectX = xDiff >= 0 ? (CurrentSprite.X * 16) + sp.MaxLeftX : (x * 16) + sp.MaxLeftX;
                        int rectY = yDiff >= 0 ? (CurrentSprite.Y * 16) + sp.MaxTopY : (y * 16) + sp.MaxTopY;
                        int width = xDiff >= 0 ? ((x * 16) + sp.MaxRightX) - ((CurrentSprite.X * 16) + sp.MaxLeftX) : ((CurrentSprite.X * 16) + sp.MaxRightX) - ((x * 16) + sp.MaxLeftX);
                        int height = yDiff >= 0 ? ((y * 16) + sp.MaxBottomY) - ((CurrentSprite.Y * 16) + sp.MaxTopY) : ((CurrentSprite.Y * 16) + sp.MaxBottomY) - ((y * 16) + sp.MaxTopY);
                        Rectangle r = new Rectangle(rectX, rectY, width, height);
                        CurrentSprite.X = x;
                        CurrentSprite.Y = y;
                        LvlView.DelayDrawing = true;
                        LvlView.SelectionRectangle = new Rectangle(CurrentSprite.X, CurrentSprite.Y, CurrentSprite.Width, CurrentSprite.Height);
                        LvlView.DelayDrawing = false;
                        LvlView.UpdateSprites(r);
                    }
                }
            }
            else if (ContinueDragging && EditMode == EditMode.Pointers && CurrentPointer != null && MouseButtons == MouseButtons.Left)
            {
                if (CurrentPointer != null)
                {
                    if (x == CurrentLevel.Width - 1 || y == CurrentLevel.Height - 1) return;
                    if (CurrentPointer.XEnter == x && CurrentPointer.YEnter == y) return;
                    int oldX = CurrentPointer.XEnter;
                    int oldY = CurrentPointer.YEnter;
                    LvlView.DelayDrawing = true;
                    CurrentPointer.XEnter = x;
                    CurrentPointer.YEnter = y;
                    LvlView.UpdatePoint(oldX, oldY);
                    LvlView.UpdatePoint(oldX + 1, oldY);
                    LvlView.UpdatePoint(oldX, oldY + 1);
                    LvlView.UpdatePoint(oldX + 1, oldY + 1);
                    LvlView.UpdatePoint(x, y);
                    LvlView.UpdatePoint(x + 1, y);
                    LvlView.UpdatePoint(x, y + 1);
                    LvlView.UpdatePoint(x + 1, y + 1);
                    LvlView.DelayDrawing = false;
                    LvlView.SelectionRectangle = new Rectangle(CurrentPointer.XEnter, CurrentPointer.YEnter, 2, 2);
                    PntEditor.UpdatePosition();
                }
            }
            else
            {
                ContinueDragging = ContinueDrawing = false;
            }
        }

        private void LvlView_MouseUp(object sender, MouseEventArgs e)
        {
            if (!ContinueDrawing) return;
            int _DrawTile = 0;
            int sX, sY;

            if (e.Button == MouseButtons.Middle)
            {
                _DrawTile = (int)NumBackground.Value;
            }
            else if (DrawMode != DrawMode.Selection)
            {
                if (e.Button == MouseButtons.Left)
                {
                    _DrawTile = LeftMouseTile;
                }
                else
                {
                    _DrawTile = RightMouseTile;
                }
            }
            else
            {
                _DrawTile = LeftMouseTile;
            }
            
            if (EditMode == EditMode.Tiles)
            {
                if (DrawMode == DrawMode.Pencil)
                {
                    UndoBuffer.Add(CurrentMultiTile);
                }
                else if ((LvlView.HasSelection || LvlView.HasSelectionLine))
                {
                    switch (DrawMode)
                    {
                        case DrawMode.Rectangle:
                            sX = LvlView.SelectionRectangle.X;
                            sY = LvlView.SelectionRectangle.Y;

                            UndoBuffer.Add(new TileAreaAction(sX, sY, CurrentLevel.GetData(sX, sY, LvlView.SelectionRectangle.Width, LvlView.SelectionRectangle.Height)));

                            LvlView.DelayDrawing = true;
                            for (int y = LvlView.SelectionRectangle.Y, i = 0; i < LvlView.SelectionRectangle.Height; y++, i++)
                            {
                                for (int x = LvlView.SelectionRectangle.X, j = 0; j < LvlView.SelectionRectangle.Width; x++, j++)
                                {
                                    CurrentLevel.SetTile(x, y, (byte)_DrawTile);
                                }
                            }
                            LvlView.DelayDrawing = false;
                            LvlView.UpdateArea();
                            break;

                        case DrawMode.Outline:
                            sX = LvlView.SelectionRectangle.X;
                            sY = LvlView.SelectionRectangle.Y;

                            UndoBuffer.Add(new TileAreaAction(sX, sY, CurrentLevel.GetData(sX, sY, LvlView.SelectionRectangle.Width, LvlView.SelectionRectangle.Height)));

                            LvlView.DelayDrawing = true;
                            for (int x = LvlView.SelectionRectangle.X, i = 0; i < LvlView.SelectionRectangle.Width; i++, x++)
                            {
                                CurrentLevel.SetTile(x, LvlView.SelectionRectangle.Y, (byte)_DrawTile);
                                CurrentLevel.SetTile(x, LvlView.SelectionRectangle.Y + LvlView.SelectionRectangle.Height - 1, (byte)_DrawTile);
                            }

                            for (int y = LvlView.SelectionRectangle.Y, i = 1; i < LvlView.SelectionRectangle.Height; i++, y++)
                            {
                                CurrentLevel.SetTile(LvlView.SelectionRectangle.X, y, (byte)_DrawTile);
                                CurrentLevel.SetTile(LvlView.SelectionRectangle.X + LvlView.SelectionRectangle.Width - 1, y, (byte)_DrawTile);
                            }
                            LvlView.DelayDrawing = false;
                            LvlView.UpdateArea();
                            break;

                        case DrawMode.Line:

                            LvlView.DelayDrawing = true;
                            CurrentMultiTile = new MultiTileAction();
                            int breakAt = Math.Abs(LvlView.SelectionLine.End.X - LvlView.SelectionLine.Start.X) + 1;
                            if (LvlView.SelectionLine.End.X > LvlView.SelectionLine.Start.X)
                            {
                                if (LvlView.SelectionLine.End.Y > LvlView.SelectionLine.Start.Y)
                                {
                                    for (int i = 0; i < breakAt; i++)
                                    {
                                        if (LvlView.SelectionLine.Start.X + i >= CurrentLevel.Width || LvlView.SelectionLine.Start.Y + i >= CurrentLevel.Height) continue;
                                        sX = LvlView.SelectionLine.Start.X + i;
                                        sY = LvlView.SelectionLine.Start.Y + i;
                                        CurrentMultiTile.AddTileChange(sX, sY, CurrentLevel.LevelData[sX, sY]);
                                        CurrentLevel.SetTile(sX, sY, (byte)_DrawTile);
                                    }
                                }
                                else
                                {
                                    for (int i = 0; i < breakAt; i++)
                                    {
                                        if (LvlView.SelectionLine.Start.X + i >= CurrentLevel.Width || LvlView.SelectionLine.Start.Y - i >= CurrentLevel.Height) continue;
                                        sX = LvlView.SelectionLine.Start.X + i;
                                        sY = LvlView.SelectionLine.Start.Y = i;
                                        CurrentMultiTile.AddTileChange(sX, sY, CurrentLevel.LevelData[sX, sY]);
                                        CurrentLevel.SetTile(sX, sY, (byte)_DrawTile);
                                    }
                                }
                            }
                            else
                            {
                                if (LvlView.SelectionLine.End.Y > LvlView.SelectionLine.Start.Y)
                                {
                                    for (int i = 0; i < breakAt; i++)
                                    {
                                        if (LvlView.SelectionLine.Start.X - i >= CurrentLevel.Width || LvlView.SelectionLine.Start.Y + i >= CurrentLevel.Height) continue;
                                        sX = LvlView.SelectionLine.Start.X - i;
                                        sY = LvlView.SelectionLine.Start.Y + i;
                                        CurrentMultiTile.AddTileChange(sX, sY, CurrentLevel.LevelData[sX, sY]);
                                        CurrentLevel.SetTile(sX, sY, (byte)_DrawTile);
                                    }
                                }
                                else
                                {
                                    for (int i = 0; i < breakAt; i++)
                                    {
                                        if (LvlView.SelectionLine.Start.X - i >= CurrentLevel.Width || LvlView.SelectionLine.Start.Y - i >= CurrentLevel.Height) continue;
                                        sX = LvlView.SelectionLine.Start.X - i;
                                        sY = LvlView.SelectionLine.Start.Y - i;
                                        CurrentMultiTile.AddTileChange(sX, sY, CurrentLevel.LevelData[sX, sY]);
                                        CurrentLevel.SetTile(sX, sY, (byte)_DrawTile);
                                    }
                                }
                            }

                            UndoBuffer.Add(CurrentMultiTile);
                            LvlView.DelayDrawing = false;
                            LvlView.ClearLine();
                            break;

                        case DrawMode.Selection:
                            useTransparentTile = e.Button == MouseButtons.Right;
                            break;

                        case DrawMode.Scatter:
                            LvlView.DelayDrawing = true;
                            CurrentMultiTile = new MultiTileAction();
                            break;
                    }
                }
            }

            UpdateCoinTotalText();
        }

        private LevelPointer CurrentPointer;
        private bool ContinueDrawing;
        #endregion

        #region display options

        private void TsbGrid_CheckedChanged(object sender, EventArgs e)
        {
            LvlView.ShowGrid = TsbGrid.Checked;
        }

        private void TsbTileSpecials_CheckedChanged(object sender, EventArgs e)
        {
            BlsSelector.ShowSpecialBlocks = LvlView.ShowSpecialBlocks = TsbTileSpecials.Checked;
        }

        private void TsbStartPoint_CheckedChanged(object sender, EventArgs e)
        {
            LvlView.DisplayStartingPosition = TsbStartPoint.Checked;
        }

        private void TsbSriteSpecials_CheckedChanged(object sender, EventArgs e)
        {
            LvlView.ShowSpecialSprites = TsbSriteSpecials.Checked;
        }

        private void TsbZoom_CheckedChanged(object sender, EventArgs e)
        {
            if (TsbZoom.Checked)
            {
                LvlView.Zoom = 2;
                PnlLengthControl.Size = new Size(PnlLengthControl.Size.Width * 2, PnlLengthControl.Size.Height * 2);
            }
            else
            {
                LvlView.Zoom = 1;
                PnlLengthControl.Size = new Size(PnlLengthControl.Size.Width / 2, PnlLengthControl.Size.Height / 2);
            }
        }
        #endregion

        #region drawing modes
        private DrawMode DrawMode = DrawMode.Pencil;

        private void TsbPencil_Click(object sender, EventArgs e)
        {
            DrawMode = DrawMode.Pencil;
            TsbPencil.Checked = true;
            TsbScatter.Checked = TsbLine.Checked = TsbBucket.Checked = TsbOutline.Checked = TsbRectangle.Checked = false;
            SetMiscText(0);
        }

        private void TsbRectangle_Click(object sender, EventArgs e)
        {
            DrawMode = DrawMode.Rectangle;
            TsbRectangle.Checked = true;
            TsbScatter.Checked = TsbLine.Checked = TsbBucket.Checked = TsbOutline.Checked = TsbPencil.Checked = false;
            SetMiscText(4);
        }

        private void TsbOutline_Click(object sender, EventArgs e)
        {
            DrawMode = DrawMode.Outline;
            TsbOutline.Checked = true;
            TsbScatter.Checked = TsbLine.Checked = TsbBucket.Checked = TsbRectangle.Checked = TsbPencil.Checked = false;
            SetMiscText(5);
        }

        private void TsbBucket_Click(object sender, EventArgs e)
        {
            DrawMode = DrawMode.Fill;
            TsbBucket.Checked = true;
            TsbScatter.Checked = TsbLine.Checked = TsbOutline.Checked = TsbRectangle.Checked = TsbPencil.Checked = false;
            SetMiscText(6);
        }

        private void TsbLine_Click(object sender, EventArgs e)
        {
            DrawMode = DrawMode.Line;
            TsbLine.Checked = true;
            TsbScatter.Checked = TsbRectangle.Checked = TsbBucket.Checked = TsbOutline.Checked = TsbPencil.Checked = false;
            SetMiscText(4);
        }

        
        private void TsbScatter_Click(object sender, EventArgs e)
        {
            DrawMode = DrawMode.Scatter;
            TsbScatter.Checked = true;
            TsbLine.Checked = TsbRectangle.Checked = TsbBucket.Checked = TsbOutline.Checked = TsbPencil.Checked = false;
            SetMiscText(4);        
        }
        #endregion

        #region level header changes

        private void CmbTypes_SelectedIndexChanged(object sender, EventArgs e)
        {
            CurrentLevel.Type = (CmbTypes.SelectedItem as LevelType).InGameID;
            LvlView.SpecialDefnitions = ProjectController.SpecialManager.GetSpecialDefinition(CurrentLevel.Type);
            BlsSelector.SpecialDefnitions = ProjectController.SpecialManager.GetSpecialDefinition(CurrentLevel.Type);
            BlsSelector.CurrentDefiniton = ProjectController.BlockManager.GetDefiniton(CurrentLevel.Type);
            LvlView.CurrentDefiniton = ProjectController.BlockManager.GetDefiniton(CurrentLevel.Type);
            ProjectController.LevelManager.GetLevelInfo(CurrentLevel.Guid).LevelType = CurrentLevel.Type;
        }

        private void CmbLength_SelectedIndexChanged(object sender, EventArgs e)
        {
            CurrentLevel.Length = CmbLength.SelectedItem.ToInt();
            switch (CurrentLevel.LevelLayout)
            {
                case LevelLayout.Horizontal:
                    PnlLengthControl.Size = new Size(CurrentLevel.Length * 256 * LvlView.Zoom, 432 * LvlView.Zoom);
                    break;

                case LevelLayout.Vertical:
                    PnlLengthControl.Size = new Size(256 * LvlView.Zoom, ((CurrentLevel.Length * 240) - 64) * LvlView.Zoom);
                    break;
            }
        }

        public void SwitchObjects(PatternTable table, BlockDefinition definition, PaletteInfo palette)
        {
            BlsSelector.HaltRendering = true;
            BlsSelector.CurrentTable = table;
            BlsSelector.CurrentDefiniton = definition;
            BlsSelector.HaltRendering = false;
            BlsSelector.CurrentPalette = palette;
        }

        private void CmbGraphics_SelectedIndexChanged(object sender, EventArgs e)
        {
            CurrentTable.SetGraphicsbank(0, ProjectController.GraphicsManager.GraphicsBanks[CmbGraphics.SelectedIndex]);
            CurrentTable.SetGraphicsbank(1, ProjectController.GraphicsManager.GraphicsBanks[CmbGraphics.SelectedIndex + 1]);
            LblHexGraphics.Text = "x" + CmbGraphics.SelectedIndex.ToHexString();
        }

        private void BtnClear_Click(object sender, EventArgs e)
        {
            LvlView.DelayDrawing = true;
            for (int y = 0; y < CurrentLevel.Height; y++)
            {
                for (int x = 0; x < CurrentLevel.Width; x++)
                {
                    if (CurrentLevel.LevelData[x, y] == CurrentLevel.ClearValue)
                        CurrentLevel.SetTile(x, y, (byte)NumBackground.Value);
                }
            }

            LvlView.DelayDrawing = true;
            LvlView.Redraw();

            CurrentLevel.ClearValue = (int)NumBackground.Value;
        }

        private void CmbPalettes_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (CurrentPalette != null)
            {
                CurrentPalette.PaletteChanged -= CurrentPalette_PaletteChanged;
            }

            CurrentPalette = CmbPalettes.SelectedItem as PaletteInfo;
            CurrentPalette.PaletteChanged += new EventHandler<TEventArgs<DoubleValue<int, int>>>(CurrentPalette_PaletteChanged);
            LvlView.CurrentPalette = CurrentPalette;
            BlsSelector.CurrentPalette = CurrentPalette;
            BlvRight.CurrentPalette = BlvLeft.CurrentPalette = CurrentPalette;

            foreach (var sv in SpriteViewers)
            {
                sv.CurrentPalette = CurrentPalette;
            }
        }

        private void CurrentPalette_PaletteChanged(object sender, TEventArgs<DoubleValue<int, int>> e)
        {
            LvlView.Redraw();
            BlsSelector.Redraw();
        }

        private bool _SelectingStartPositionMode;
        private void BtnStartPoint_Click(object sender, EventArgs e)
        {
            _SelectingStartPositionMode = true;
            TabLevelInfo.Enabled = false;
            PnlDrawing.Enabled = false;
            SetMiscText(3);
        }

        #endregion

        #region sprites
        private Sprite CurrentSprite = null;

        private Sprite SelectSprite(int x, int y)
        {
            var possibleSprites = (from s in CurrentLevel.SpriteData
                                   where x >= s.X && x <= s.X + (s.Width - 1) &&
                                         y >= s.Y && y <= s.Y + (s.Height - 1)
                                   select s).FirstOrDefault();
            return possibleSprites;
        }

        private List<SpriteViewer> SpriteViewers = new List<SpriteViewer>();

        private void LoadSpriteSelector()
        {
            List<Sprite> CurrentList;
            foreach (var s in ProjectController.SpriteManager.SpriteGroups.Keys)
            {
                foreach (var k in from l in ProjectController.SpriteManager.SpriteGroups[s].Keys orderby l select l)
                {
                    if (k == "Map") continue;
                    SpriteViewer spViewer = new SpriteViewer(ProjectController.SpriteManager.SpriteGroups[s][k].Count);
                    spViewer.SpecialPalette = ProjectController.SpecialManager.SpecialPalette;
                    CurrentList = new List<Sprite>();

                    int x = 0;
                    foreach (var ks in ProjectController.SpriteManager.SpriteGroups[s][k])
                    {
                        Sprite next = new Sprite();
                        next.X = x;
                        next.Y = 0;
                        next.InGameID = ks.InGameId;
                        CurrentList.Add(next);
                        x += next.Width + 1;
                    }

                    spViewer.SpriteList = CurrentList;
                    spViewer.Location = new Point(0, 0);
                    SpriteViewers.Add(spViewer);
                    spViewer.CurrentPalette = CurrentPalette;
                    spViewer.UpdateSprites();
                    spViewer.SelectionChanged += new EventHandler<TEventArgs<Sprite>>(spViewer_SelectionChanged);

                    TabPage tPage = new TabPage();
                    tPage.Text = k;
                    tPage.AutoScroll = true;
                    tPage.Controls.Add(spViewer);


                    switch (s)
                    {
                        case 1:
                            TabClass1.TabPages.Add(tPage);
                            break;

                        case 2:
                            TabClass2.TabPages.Add(tPage);
                            break;

                        case 3:
                            TabClass3.TabPages.Add(tPage);
                            break;
                    }
                }
            }
        }

        private Sprite CurrentSelectorSprite;

        private void spViewer_SelectionChanged(object sender, TEventArgs<Sprite> e)
        {
            CurrentSelectorSprite = e.Data;
            if (CurrentSelectorSprite != null)
            {
                foreach (var sp in SpriteViewers)
                {
                    if (sp.SelectedSprite != CurrentSelectorSprite)
                    {
                        sp.SelectedSprite = null;
                    }
                }
                LblSpriteSelected.Text = "Sprite: " + CurrentSelectorSprite.InGameID.ToHexString() + " - " + CurrentSelectorSprite.Name;
            }
            else
            {
                LblSpriteSelected.Text = "None";
            }
        }

        #endregion

        #region blocks
        public BlockLayout CurrentLayout { get; private set; }

        void BlockManager_DefinitionsSaved(object sender, EventArgs e)
        {
            UpdateGraphics();
        }
        #endregion

        #region form gui
        private void BtnShowHideInfo_Click(object sender, EventArgs e)
        {
            if (TabLevelInfo.Visible)
            {
                PnlInfo.Height = 30;
                TabLevelInfo.Visible = false;
                BtnShowHideInfo.Image = Properties.Resources.up;
            }
            else
            {
                PnlInfo.Height = 160;
                TabLevelInfo.Visible = true;
                BtnShowHideInfo.Image = Properties.Resources.down;
            }
        }

        private void CmbLayouts_SelectedIndexChanged(object sender, EventArgs e)
        {
            CurrentLayout = CmbLayouts.SelectedItem as BlockLayout;
            BlsSelector.BlockLayout = CurrentLayout;
        }

        private void TabEditSelector_SelectedIndexChanged(object sender, EventArgs e)
        {
            switch (TabEditSelector.SelectedIndex)
            {
                case 0:
                    EditMode = EditMode.Tiles;
                    switch (DrawMode)
                    {
                        case DrawMode.Pencil:
                            SetMiscText(0);
                            break;

                        case DrawMode.Rectangle:
                            SetMiscText(4);
                            break;

                        case DrawMode.Outline:
                            SetMiscText(5);
                            break;

                        case DrawMode.Fill:
                            SetMiscText(6);
                            break;

                        case DrawMode.Scatter:
                            SetMiscText(6);
                            break;
                    }
                    TlsDrawing.Visible = true;
                    break;
                case 1:
                    EditMode = EditMode.Sprites;
                    SetMiscText(1);
                    TlsDrawing.Visible = false;
                    break;

                case 2:
                    EditMode = EditMode.Pointers;
                    TlsDrawing.Visible = false;
                    SetMiscText(2);
                    break;
            }
        }

        private void LvlView_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            if (e.Modifiers == Keys.Control)
            {
                switch (e.KeyCode)
                {
                    case Keys.Add:
                        if (!TsbZoom.Checked)
                            TsbZoom.Checked = true;
                        break;

                    case Keys.Subtract:
                        if (TsbZoom.Checked)
                            TsbZoom.Checked = false;
                        break;

                    case Keys.S:
                        Save();
                        break;

                    case Keys.G:
                        TsbGrid.Checked = !TsbGrid.Checked;
                        break;

                    case Keys.X:
                        Cut();
                        break;

                    case Keys.C:
                        Copy();
                        break;

                    case Keys.V:
                        Paste(useTransparentTile);
                        break;

                    case Keys.W:
                        TsbTileSpecials.Checked = !TsbTileSpecials.Checked;
                        break;

                    case Keys.E:
                        TsbSriteSpecials.Checked = !TsbSriteSpecials.Checked;
                        break;

                    case Keys.R:
                        TsbStartPoint.Checked = !TsbStartPoint.Checked;
                        break;

                    case Keys.F:
                        TsbStartPoint.Checked = true;
                        BtnStartPoint_Click(null, null);
                        break;

                    case Keys.D:
                        ToggleRightClickMode();
                        break;

                    case Keys.Z:
                        Undo();
                        break;
                }
            }
            else
            {
                switch (e.KeyCode)
                {
                    case Keys.Delete:

                        if (CurrentSprite != null && EditMode == EditMode.Sprites)
                        {
                            CurrentLevel.SpriteData.Remove(CurrentSprite);
                            LvlView.DelayDrawing = true;
                            LvlView.ClearSelection();
                            LvlView.DelayDrawing = false;
                            LvlView.UpdateSprites();
                            CurrentSprite = null;
                        }
                        else if (EditMode == EditMode.Tiles && DrawMode == DrawMode.Selection)
                        {
                            DeleteTiles();
                        }
                        else if (EditMode == EditMode.Pointers)
                        {
                            DeleteCurrentPointer();
                        }
                        break;

                    case Keys.Escape:
                        ContinueDrawing = false;
                        LvlView.ClearSelection();
                        LvlView.ClearLine();
                        if (DrawMode == DrawMode.Selection)
                            DrawMode = PreviousMode;
                        break;
                }
            }
        }

        private void ToggleRightClickMode()
        {
            MouseMode = MouseMode == MouseMode.RightClickSelection ? MouseMode.RightClickTile : MouseMode.RightClickSelection;
            LblRightClickMode.Text = "Right Click Mode: " + (MouseMode == MouseMode.RightClickSelection ? "Selector" : "Tile Placement");
        }

        private byte[,] TileBuffer;

        private void DeleteTiles()
        {
            int sX = LvlView.SelectionRectangle.X;
            int sY = LvlView.SelectionRectangle.Y;
            UndoBuffer.Add(new TileAreaAction(sX, sY, CurrentLevel.GetData(sX, sY, LvlView.SelectionRectangle.Width, LvlView.SelectionRectangle.Height)));
            LvlView.DelayDrawing = true;
            for (int y = sY, i = 0; i < LvlView.SelectionRectangle.Height; y++, i++)
            {
                for (int x = sX, j = 0; j < LvlView.SelectionRectangle.Width; x++, j++)
                {
                    CurrentLevel.SetTile(x, y, (byte)NumBackground.Value);
                }
            }
            LvlView.DelayDrawing = false;
            LvlView.UpdateArea();
        }

        private void Cut()
        {
            Copy();
            DeleteTiles();
            UpdateCoinTotalText();
        }

        private void Copy()
        {
            TileBuffer = CurrentLevel.GetData(LvlView.SelectionRectangle.X, LvlView.SelectionRectangle.Y, LvlView.SelectionRectangle.Width, LvlView.SelectionRectangle.Height);
        }

        private void Paste(bool transparentTile)
        {
            LvlView.DelayDrawing = true;
            Rectangle usedRectangle = LvlView.SelectionRectangle;

            if (LvlView.SelectionRectangle.Width == 1 && LvlView.SelectionRectangle.Height == 1)
            {
                usedRectangle.Width = TileBuffer.GetLength(0);
                usedRectangle.Height = TileBuffer.GetLength(1);
            }

            int sX = usedRectangle.X;
            int sY = usedRectangle.Y;
            UndoBuffer.Add(new TileAreaAction(sX, sY, CurrentLevel.GetData(sX, sY, usedRectangle.Width, usedRectangle.Height)));

            for (int j = 0; j < usedRectangle.Height; j++)
            {
                for (int i = 0; i < usedRectangle.Width; i++)
                {
                    if (transparentTile && TileBuffer[i % TileBuffer.GetLength(0), j % TileBuffer.GetLength(1)] == NumBackground.Value) continue;
                    CurrentLevel.SetTile(usedRectangle.X + i, usedRectangle.Y + j, TileBuffer[i % TileBuffer.GetLength(0), j % TileBuffer.GetLength(1)]);
                }
            }
            LvlView.DelayDrawing = false;
            LvlView.UpdateArea(usedRectangle);
            UpdateCoinTotalText();
        }

        private void TabCoins_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.DrawLine(Pens.Black, 25, 65, 270, 65);
        }

        public EditMode EditMode { get; set; }
        private int PreviousTextIndex = 0;
        private int CurrentTextIndex = 0;
        public void SetMiscText(int index)
        {
            switch (index)
            {
                case 0:
                    LblMisc.Text = "Left Mouse Button: Place tile; Right Mouse Button: Erase tile; Shift + Left Mouse: Set tile on level as currently selected tile";
                    break;

                case 1:
                    LblMisc.Text = "Left Mouse Button: Select sprite; Right Mouse Button: Add or replace sprite. Delete will remove the sprite.";
                    break;

                case 2:
                    LblMisc.Text = "Left Mouse Button: Select pointer. To change entry point, click and drag pointer";
                    break;

                case 3:
                    LblMisc.Text = "Click on the level screen to set starting position";
                    break;

                case 4:
                    LblMisc.Text = "Click and drag to create a area to fill. Use the right mouse button to erase instead.";
                    break;

                case 5:
                    LblMisc.Text = "Click and drag to select an area to outline. Use the right mouse button to erase instead.";
                    break;

                case 6:
                    LblMisc.Text = "Click on any area to fill an enclosed area. Use the right mouse button to erase instead.";
                    break;
            }

            PreviousTextIndex = CurrentTextIndex;
            CurrentTextIndex = index;
        }


        private void BtnLevelSize_Click(object sender, EventArgs e)
        {
            LblSpriteSize.Text = "Sprite Data Size: " + (((CurrentLevel.SpriteData.Count) * 3) + 1).ToString() + " bytes";
            LblLevelSize.Text = "Level Data Size: " + (CurrentLevel.GetCompressedData().Length + 12 + (CurrentLevel.Pointers.Count * 9)).ToString() + " bytes";
        }

        public Bitmap GetLevelBitmap()
        {
            Bitmap bitmap = new Bitmap(PnlLengthControl.Bounds.Width, PnlLengthControl.Bounds.Height);
            PnlLengthControl.DrawToBitmap(bitmap, new Rectangle(0, 0, PnlLengthControl.Width, PnlLengthControl.Height));
            return bitmap;
        }

        private void TsbSave_Click(object sender, EventArgs e)
        {
            Save();
            //ReubenController.SaveTestLevel(CurrentLevel);
        }

        private void Save()
        {
            CurrentLevel.StartAction = CmbActions.SelectedIndex;
            CurrentLevel.ClearValue = (int)NumBackground.Value;
            CurrentLevel.GraphicsBank = CmbGraphics.SelectedIndex;
            CurrentLevel.Palette = CmbPalettes.SelectedIndex;
            CurrentLevel.Time = (int)NumTime.Value;
            CurrentLevel.Type = CmbTypes.SelectedIndex + 1;
            CurrentLevel.Music = CmbMusic.SelectedIndex;
            CurrentLevel.StartAction = CmbActions.SelectedIndex;
            CurrentLevel.ScrollType = CmbScroll.SelectedIndex;
            CurrentLevel.Save();
            MessageBox.Show("Level succesfully saved.");
        }
        #endregion

        #region pointers
        private void BtnAddPointer_Click(object sender, EventArgs e)
        {
            CurrentLevel.AddPointer();
            PntEditor.CurrentPointer = CurrentLevel.Pointers[CurrentLevel.Pointers.Count - 1];
            LvlView.DelayDrawing = true;
            LvlView.UpdatePoint(0, 0);
            LvlView.UpdatePoint(1, 0);
            LvlView.UpdatePoint(0, 1);
            LvlView.UpdatePoint(1, 1);
            LvlView.DelayDrawing = false;
            LvlView.SelectionRectangle = new Rectangle(0, 0, 2, 2);
            CurrentPointer = PntEditor.CurrentPointer;
            BtnAddPointer.Enabled = CurrentLevel.Pointers.Count < 4;
        }

        private void BtnDeletePointer_Click(object sender, EventArgs e)
        {
            DeleteCurrentPointer();
        }

        private void DeleteCurrentPointer()
        {
            if (CurrentPointer != null)
            {
                LvlView.DelayDrawing = true;
                CurrentLevel.Pointers.Remove(CurrentPointer);
                LvlView.UpdatePoint(CurrentPointer.XEnter, CurrentPointer.YEnter);
                LvlView.UpdatePoint(CurrentPointer.XEnter, CurrentPointer.YEnter + 1);
                LvlView.UpdatePoint(CurrentPointer.XEnter + 1, CurrentPointer.YEnter);
                LvlView.UpdatePoint(CurrentPointer.XEnter + 1, CurrentPointer.YEnter + 1);
                LvlView.DelayDrawing = false;
                LvlView.ClearSelection();
                PntEditor.CurrentPointer = null;
                BtnDeletePointer.Enabled = false;
                BtnAddPointer.Enabled = true;
            }
        }
        #endregion

        private void BlsSelector_DoubleClick(object sender, EventArgs e)
        {
            ReubenController.OpenBlockEditor(CurrentLevel.Type, LeftMouseTile, CmbGraphics.SelectedIndex, CurrentLevel.AnimationBank, CmbPalettes.SelectedIndex);
        }

        int PreviousSelectorX, PreviousSelectorY;
        private void BlsSelector_MouseMove(object sender, MouseEventArgs e)
        {
            int x = e.X / 16;
            int y = e.Y / 16;
            int index =(e.X / 16) + ((e.Y / 16) * 16);
            if(index > 255) return;
            if (PreviousSelectorX == x && PreviousSelectorY == y) return;
            PreviousSelectorX = x;
            PreviousSelectorY = y;
            int tile = BlsSelector.BlockLayout.Layout[index];
            LblSelectorHover.Text = "Block: " + tile.ToHexString();
            LevelToolTip.SetToolTip(BlsSelector, ProjectController.BlockManager.GetBlockString(CurrentLevel.Type, tile));
        }

        public int DrawingTile
        {
            get
            {
                if (MouseButtons == MouseButtons.Middle)
                    return (int)NumBackground.Value;

                if(DrawMode != DrawMode.Selection)
                {
                    if (MouseButtons == MouseButtons.Left)
                        return LeftMouseTile;
                    else
                        return RightMouseTile;
                }

                return LeftMouseTile;
            }
        }

        private void LblRightClickMode_Click(object sender, EventArgs e)
        {
            ToggleRightClickMode();
        }

        private void toolStripMenuItem6_Click(object sender, EventArgs e)
        {
            PnlHorizontalGuide.Guide1.Visible = PnlHorizontalGuide.Guide2.Visible = false;
            LvlView.UpdateGuide(Orientation.Horizontal, 1);
            LvlView.UpdateGuide(Orientation.Horizontal, 2);
        }

        private void freeGuide2_Click(object sender, EventArgs e)
        {
            PnlHorizontalGuide.GuideSnapMode = GuideMode.Free;
            freeGuide2.Checked = true;
            snapToScreenLengthToolStripMenuItem.Checked =
            snapToFullMeterJumpLengthToolStripMenuItem.Checked =
            snapToRunningJumpHeightToolStripMenuItem.Checked =
            snapToWalkingJumpLengthToolStripMenuItem.Checked =
            snapToJumpLengthToolStripMenuItem.Checked = false;
        }

        private void TsbProperties_CheckStateChanged(object sender, EventArgs e)
        {
            BlsSelector.ShowBlockProperties = LvlView.ShowBlockProperties = TsbProperties.Checked;
        }

        private void LevelEditor_FormClosed(object sender, FormClosedEventArgs e)
        {
            ProjectController.SettingsManager.SetLevelSetting(CurrentLevel.Guid, "ShowGrid", TsbGrid.Checked);
            ProjectController.SettingsManager.SetLevelSetting(CurrentLevel.Guid, "SpecialTiles", TsbTileSpecials.Checked);
            ProjectController.SettingsManager.SetLevelSetting(CurrentLevel.Guid, "SpecialSprites", TsbSriteSpecials.Checked);
            ProjectController.SettingsManager.SetLevelSetting(CurrentLevel.Guid, "BlockProperties", TsbProperties.Checked);
            ProjectController.SettingsManager.SetLevelSetting(CurrentLevel.Guid, "ShowStart", TsbStartPoint.Checked);
            ProjectController.SettingsManager.SetLevelSetting(CurrentLevel.Guid, "Zoom", TsbZoom.Checked);
            ProjectController.SettingsManager.SetLevelSetting(CurrentLevel.Guid, "Draw", DrawMode.ToString());
            ProjectController.SettingsManager.SetLevelSetting(CurrentLevel.Guid, "Layout", CmbLayouts.SelectedIndex);
            ProjectController.Save();
        }

        private List<IUndoableAction> UndoBuffer;
        private List<IUndoableAction> RedoBuffer;
        private MultiTileAction CurrentMultiTile;

        private void Undo()
        {
            if (UndoBuffer.Count == 0) return;
            IUndoableAction action = UndoBuffer[UndoBuffer.Count - 1];
            switch (action.Type)
            {
                case ActionType.TileArea:
                    UndoTileArea((TileAreaAction)action);
                    break;

                case ActionType.MultiTile:
                    UndoMultiTile((MultiTileAction)action);
                    break;
            }
        }

        private void UndoTileArea(TileAreaAction action)
        {
            LvlView.DelayDrawing = true;
            Rectangle usedRectangle = new Rectangle(action.X, action.Y, action.Data.GetLength(0), action.Data.GetLength(1));

            int sX = usedRectangle.X;
            int sY = usedRectangle.Y;
            RedoBuffer.Add(action);

            for (int j = 0; j < usedRectangle.Height; j++)
            {
                for (int i = 0; i < usedRectangle.Width; i++)
                {
                    CurrentLevel.SetTile(usedRectangle.X + i, usedRectangle.Y + j, action.Data[i ,j]);
                }
            }
            LvlView.DelayDrawing = false;
            LvlView.UpdateArea(usedRectangle);
            UpdateCoinTotalText();
            UndoBuffer.Remove(action);
        }

        private void UndoMultiTile(MultiTileAction action)
        {
            RedoBuffer.Add(action);
            UndoBuffer.Remove(action);

            LvlView.DelayDrawing = true;
            foreach (SingleTileChange stc in action.TileChanges.Reverse<SingleTileChange>())
            {
                CurrentLevel.SetTile(stc.X, stc.Y, (byte)stc.Tile);
            }
            LvlView.DelayDrawing = false;
            LvlView.UpdateArea(action.InvalidArea);
        }

        private void changeGuideColorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ColorDialog cDialog = new ColorDialog();
            cDialog.Color = ProjectController.SettingsManager.GetLevelSetting<Color>(CurrentLevel.Guid, "VGuideColor");
            if (cDialog.ShowDialog() == DialogResult.OK)
            {
                ProjectController.SettingsManager.SetLevelSetting(CurrentLevel.Guid, "VGuideColor", cDialog.Color);
                PnlVerticalGuide.GuideColor = cDialog.Color;
            }
        }

        private void changeGuideColorToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            ColorDialog cDialog = new ColorDialog();
            cDialog.Color = ProjectController.SettingsManager.GetLevelSetting<Color>(CurrentLevel.Guid, "HGuideColor");
            if (cDialog.ShowDialog() == DialogResult.OK)
            {
                ProjectController.SettingsManager.SetLevelSetting(CurrentLevel.Guid, "HGuideColor", cDialog.Color);
                PnlHorizontalGuide.GuideColor = cDialog.Color;
            }
        }

        private void NumSpecials_ValueChanged(object sender, EventArgs e)
        {
            ProjectController.SettingsManager.SetLevelSetting(CurrentLevel.Guid, "TransSpecials", (double) NumSpecials.Value);
            LvlView.FullUpdate();
        }

        private void NumProperties_ValueChanged(object sender, EventArgs e)
        {
            ProjectController.SettingsManager.SetLevelSetting(CurrentLevel.Guid, "TransProps", (double) NumProperties.Value);
            LvlView.FullUpdate();
        }
    }

    public enum DrawMode
    {
        Pencil,
        Line,
        Fill,
        Rectangle,
        Outline,
        Scatter,
        Selection
    }

    public enum EditMode
    {
        Tiles,
        Sprites,
        Pointers
    }

    public enum MouseMode
    {
        RightClickSelection,
        RightClickTile
    }
}