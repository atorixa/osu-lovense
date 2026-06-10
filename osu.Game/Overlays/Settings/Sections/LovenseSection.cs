using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Overlays.Settings.Sections.Lovense;

namespace osu.Game.Overlays.Settings.Sections
{
    public partial class LovenseSection : SettingsSection
    {
        public override LocalisableString Header => "Lovense";
        public override Drawable CreateIcon() => new SpriteIcon { Icon = FontAwesome.Solid.Plug };

        public LovenseSection()
        {
            Children = new Drawable[]
            {
                new LovenseSettingsSubsection()
            };
        }
    }
}
