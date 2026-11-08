using Dawnsbury.Core;
using Dawnsbury.Core.Mechanics;
using Dawnsbury.Core.Mechanics.Core;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Core.Mechanics.Treasure;
using Dawnsbury.Core.Roller;
using Dawnsbury.Core.StatBlocks.Monsters.L15;
using Dawnsbury.Display.Illustrations;
using Dawnsbury.Modding;

namespace Dawnsbury.Mods.Phoenix;

public class MirrorEntityTemplate
{
    private static QEffect MonsterPanache()
    {
        return new QEffect("Panache", "You have a +1 status bonus to all skill checks until your next Strike.", new ModdedIllustration("PhoenixAssets/panache.PNG"))
        {
            Id = AddSwash.PanacheId,
            Key = "panache",
            YouBeginAction = async (qfPanache, strike) =>
            {
                if (strike.HasTrait(Trait.Strike)) qfPanache.ExpiresAt = ExpirationCondition.Immediately;
            },
            BonusToSkillChecks = (_, _, _) => new Bonus(1, BonusType.Status, "panache")
        };
    }
    public static void RegisterMirrorSwashbuckler()
    {
        MirrorEntity.RegisterClassTemplate(AddSwash.SwashTrait, MirrorEntity.MirrorEntityBaseStatblock.Rogue, (entity) =>
        {
            entity.MaxHP += 2;
            entity.Abilities.Dexterity++;
            entity.Abilities.Charisma++;
            entity.Abilities.Wisdom--;
            entity.Skills.Set(Skill.Intimidation, 25);

            entity.AddQEffect(new QEffect("Panache", "When the swashbuckler succeeds on a skill check, it gains panache, granting a +1 status bonus to all skill checks after its next Strike.")
            {
                AfterYouTakeActionAgainstTarget = async (qf, action, target, result) =>
                {
                    if (result >= CheckResult.Success && !action.HasTrait(Trait.Strike))
                    {
                        qf.Owner.AddQEffect(MonsterPanache());
                    }
                }
            });

            entity.AddQEffect(new QEffect("Precise Strike", "The swashbuckler deals an extra 4d6 precision damage with its Strikes.")
            {
                YourStrikeMayDealPrecisionDamage = (qf, _, _) => DiceFormula.FromText("4d6", "precise strike")
            });
            entity.WithFeat(AddSwash.OpportuneRiposte.FeatName);
            entity.AddQEffect(new QEffect("Vivacious Speed", "The swashbuckler has a +10-foot status bonus to Speed (increases to 20-foot while the swashbuckler has panache).")
            {
                BonusToAllSpeeds = (qf) => qf.Owner.HasEffect(AddSwash.PanacheId) ? new Bonus(4, BonusType.Status, "vivacious speed") : new Bonus(2, BonusType.Status, "vivacious speed")
            });
            entity.WithFeat(AddSwash.CharmedLife.FeatName);
            entity.AddQEffect(new QEffect("Finishing Follow-Through", "When the swashbuckler reduces an enemy to 0 HP, it gains panache.")
            {
                AfterYouDealDamage = async (you, action, target) =>
                {
                    if (target.HP <= 0)
                    {
                        you.AddQEffect(MonsterPanache());
                    }
                }
            });
            entity.WithFeat(AddSwash.ReflexiveRiposte.FeatName);
        });
    }
}