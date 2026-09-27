using Dawnsbury.Core;
using Dawnsbury.Modding;
using Dawnsbury.Core.Mechanics.Treasure;
using Dawnsbury.Core.StatBlocks.Monsters.L15;

namespace Dawnsbury.Mods.Phoenix;

public class MirrorEntityTemplate
{
    public static void RegisterMirrorSwashbuckler()
    {
        MirrorEntity.RegisterClassTemplate(AddSwash.SwashTrait, MirrorEntity.MirrorEntityBaseStatblock.Rogue, (entity) =>
        {
            entity.WithFeat(AddSwash.PreciseStrike.FeatName);
            entity.WithFeat(AddSwash.VivaciousSpeed.FeatName);
            entity.WithFeat(AddSwash.CharmedLife.FeatName);
            entity.WithFeat(AddSwash.BleedingFinisher.FeatName);
        });
    }
}