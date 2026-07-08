using Dawnsbury.Core;
using Dawnsbury.Core.CharacterBuilder;
using Dawnsbury.Core.CharacterBuilder.Feats;
using Dawnsbury.Core.CharacterBuilder.FeatsDb;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.TrueFeatDb.Archetypes;
using Dawnsbury.Core.CombatActions;
using Dawnsbury.Core.Creatures;
using Dawnsbury.Core.Mechanics;
using Dawnsbury.Core.Mechanics.Core;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Core.Mechanics.Rules;
using Dawnsbury.Core.Mechanics.Treasure;
using Dawnsbury.Core.Mechanics.Targeting;
using Dawnsbury.Display.Illustrations;
using Dawnsbury.Modding;
using Dawnsbury.Display;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Dawnsbury.Mods.Phoenix;

public class AddMulticlassSwash
{
    public static IEnumerable<Feat> GetSwashArchetypeSubclasses()
    {
        foreach (var style in AllFeats.GetFeatByFeatName(AddSwash.Swashbuckler.FeatName).Subfeats!)
        {
            var style2 = style as AddSwash.SwashbucklerStyle;
            yield return new Feat(ModManager.RegisterFeatName(style.FeatName.ToStringOrTechnical() + "ForArchetype", style.Name), style.FlavorText, "You can choose to become trained in " + style2.Skill.ToString() + ". You gain panache" + style.RulesText.Split("panache")[1].Split("\n")[0], [], null)
                .WithOnSheet(delegate (CalculatedCharacterSheetValues sheet)
                {
                    sheet.TrainInThisOrThisOrSubstitute(Skill.Acrobatics, style2.Skill);
                    sheet.AddFeatForPurposesOfPrerequisitesOnly(style.FeatName);
                })
                .WithPermanentQEffect(null, qf =>
                {
                    qf.CharacterSheetBecomesCreature = (sheet, creature) =>
                    {
                        QEffect panacheGranter = creature.QEffects.First((fct) => fct.Key == "PanacheGranter");
                        List<ActionId> list = (List<ActionId>)panacheGranter.Tag;
                        foreach (ActionId id in style2.PanacheTriggers)
                        {
                            list.Add(id);
                            panacheGranter.Description += ", " + id.HumanizeTitleCase2();
                        }
                    };
                });
        }
    }

    public static CombatAction CreateBasicFinisher(Creature swash, Item item, bool thrown, StrikeModifiers modifiers)
    {
        CombatAction basicFinisher = StrikeRules.CreateStrike(swash, item, thrown ? RangeKind.Ranged : RangeKind.Melee, -1, thrown, modifiers)
            .WithActionCost(1)
            .WithExtraTrait(AddSwash.Finisher)
            .WithExtraTrait(Trait.Basic)
            .WithDescription(StrikeRules.CreateBasicStrikeDescription2(modifiers, null, null, null, null, "You lose panache, whether the attack succeeds or fails."))
            .WithEffectOnSelf(async (spell, self) =>
            {
                AddSwash.FinisherExhaustion(self);
            });
        basicFinisher.Name = "Basic Finisher";
        basicFinisher.Illustration = new SideBySideIllustration(item.Illustration, IllustrationName.StarHit);
        return basicFinisher;
    }

    public static Feat MulticlassSwashDedication = ArchetypeFeats.CreateMulticlassDedication(AddSwash.SwashTrait, 
            "You've learned to move and fight with style and swagger.", "Choose a swashbuckler style. You gain the panache class feature, and can gain panache in all the ways a swashbuckler of your style can. You become trained in Acrobatics or the skill associated with your style. You also become trained in swashbuckler class DC. You don't gain any other effects of your chosen style.", GetSwashArchetypeSubclasses().ToList()).WithDemandsAbility14(Ability.Dexterity).WithDemandsAbility14(Ability.Charisma)
        .WithOnCreature(swash =>
        {
            swash.AddQEffect(AddSwash.PanacheGranter());
        });

    public static Feat FinishingPrecision = new TrueFeat(ModManager.RegisterFeatName("FinishingPrecision", "Finishing Precision"), 4, 
            "You've learned how to land daring blows when you have panache.", "When you have panache and make a Strike with a melee agile or finesse weapon or an agile or finesse unarmed strike, you deal 1 extra damage. This damage is 1d6 instead if the Strike was part of a finisher. This damage doesn't increase as you gain levels. In addition, you gain the Basic Finisher action.", [])
        .WithAvailableAsArchetypeFeat(AddSwash.SwashTrait)
        .WithRulesBlockForCombatAction(swash =>
        {
            CombatAction exampleFinisher = CombatAction.CreateSimple(swash, "Basic Finisher", [ AddSwash.Finisher ]).WithActionCost(1);
            exampleFinisher.Description = "You make a graceful, deadly attack. Make a Strike; if you hit and your weapon qualifies for precise strike, you deal the full 1d6 damage from precise strike.";
            return exampleFinisher;
        })
        .WithOnSheet(sheet =>
        {
            sheet.AddFeatForPurposesOfPrerequisitesOnly(AddSwash.PreciseStrike);
        })
        .WithOnCreature(creature =>
        {
            creature.AddQEffect(AddSwash.PreciseStrikeEffect(1));
        })
        .WithPermanentQEffect(null, qf =>
        {
            qf.ProvideStrikeModifier = item =>
            {
                StrikeModifiers basic = new StrikeModifiers();
                bool flag = !item.HasTrait(Trait.Ranged) && (item.HasTrait(Trait.Agile) || item.HasTrait(Trait.Finesse));
                bool flag2 = qf.Owner.HasEffect(AddSwash.PanacheId);
                if (flag && flag2)
                {
                    return CreateBasicFinisher(qf.Owner, item, false, basic);
                }
                else return null;
            };
        });

    public static Feat SwashbucklersRiposte = new TrueFeat(ModManager.RegisterFeatName("SwashbucklersRiposte", "Swashbuckler's Riposte"), 6, 
            "You've learned to riposte against ill-conceived attacks.", "When an enemy critically fails its Strike against you, you can use your reaction to make a melee Strike against that enemy or make a Disarm attempt.", [])
        .WithActionCost(Constants.ACTION_COST_REACTION)
        .WithAvailableAsArchetypeFeat(AddSwash.SwashTrait)
        .WithOnSheet(sheet =>
        {
            sheet.AddFeat(AddSwash.OpportuneRiposte!, null);
        });

    public static Feat SwashbucklersSpeed = new TrueFeat(ModManager.RegisterFeatName("SwashbucklersSpeed", "Swashbuckler's Speed"), 8, 
            "You move faster, with or without panache.", "Increase the status bonus to your Speeds when you have panache to a +10-foot status bonus; you also gain a +5-foot status bonus to your Speeds when you don't have panache.", [])
        .WithAvailableAsArchetypeFeat(AddSwash.SwashTrait)
        .WithPermanentQEffect(qf =>
        {
            qf.BonusToAllSpeeds = qf2 => new Bonus(1, BonusType.Status, "Swashbuckler's Speed");
            qf.YouAcquireQEffect = (qfThis, qfGet) =>
            {
                if (qfGet.Id == AddSwash.PanacheId)
                {
                    QEffect qfNew = qfGet;
                    qfNew.BonusToAllSpeeds = qf3 => new Bonus(2, BonusType.Status, "Panache");
                    qfNew.Description = qfNew.Description!.Replace("+5-foot", "+10-foot");
                    return qfNew;
                }
                else return qfGet;
            };
        });
    
    public static TrueFeat SwashbucklerEvasiveness = ArchetypeFeats.DuplicateFeatAsArchetypeFeat(FeatName.Evasiveness, AddSwash.SwashTrait, 12);

    public static void LoadMulticlassSwash()
    {
        ModManager.AddFeat(MulticlassSwashDedication);
        foreach (Feat ft in ArchetypeFeats.CreateBasicAndAdvancedMulticlassFeatGrantingArchetypeFeats(AddSwash.SwashTrait, "Flair"))
        {
            ModManager.AddFeat(ft);
        };
        ModManager.AddFeat(FinishingPrecision);
        ModManager.AddFeat(SwashbucklersRiposte);
        ModManager.AddFeat(SwashbucklersSpeed);
        ModManager.AddFeat(SwashbucklerEvasiveness);
    }
}
