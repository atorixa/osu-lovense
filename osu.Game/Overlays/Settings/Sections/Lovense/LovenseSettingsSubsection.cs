// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterfaceV2;

namespace osu.Game.Overlays.Settings.Sections.Lovense
{
    public partial class LovenseSettingsSubsection : SettingsSubsection
    {
        protected override LocalisableString Header => "Intiface";

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            Children = new Drawable[]
            {

                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Masking = true,
                    CornerRadius = 8,
                    Margin = new MarginPadding { Bottom = 10 },
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = Colour4.FromHex("#5E4D79")
                        },
                        new OsuTextFlowContainer(cp => cp.Font = OsuFont.Default.With(weight: FontWeight.SemiBold))
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Padding = new MarginPadding(15),
                            Text = "Integration - made with <3 by atorixa"
                        }
                    }
                },

                new SettingsItemV2(new FormCheckBox
                {
                    Caption = "Enable support",
                    Current = config.GetBindable<bool>(lookup: OsuSetting.LovenseEnabled)
                }),

                new SettingsItemV2(new FormTextBox
                {
                    Caption = "Intiface websocket URL",
                    Current = config.GetBindable<string>(lookup: OsuSetting.IntifaceUrl)
                }),
                new SettingsItemV2(new FormSliderBar<int>
                {
                    Caption = "Vibration intensity",
                    Current = config.GetBindable<int>(lookup: OsuSetting.LovenseIntensity),
                    KeyboardStep = 1
                })
            };
        }
    }
}
