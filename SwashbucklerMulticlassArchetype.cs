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
using Dawnsbury.Display.Illustrations;
using Dawnsbury.Modding;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Dawnsbury.Mods.Phoenix;

public class AddMulticlassSwash
{
    public static IEnumerable<Feat> GetSwashArchetypeSubclasses()
    {
        foreach (var style in AllFeats.All.First((Feat ft) => ft.FeatName == AddSwash.Swashbuckler.FeatName).Subfeats)
        {
            var style2 = style as AddSwash.SwashbucklerStyle;
            yield return new Feat(ModManager.RegisterFeatName(style.FeatName.ToStringOrTechnical() + "ForArchetype", style.Name), style.FlavorText, "You can choose to become trained in " + style2.Skill.ToString() + ". You gain " + style.RulesText.Substring(style.RulesText.IndexOf("panache")), new List<Trait>(), null)
                .WithOnSheet(delegate (CalculatedCharacterSheetValues sheet)
                {
                    sheet.TrainInThisOrThisOrSubstitute(Skill.Acrobatics, style2.Skill);
                    sheet.AddFeatForPurposesOfPrerequisitesOnly(style);
                });
        }
    }

    public static Feat MulticlassSwashDedication = ArchetypeFeats.CreateMulticlassDedication(AddSwash.SwashTrait, "You've learned to move and fight with style and swagger.", "Choose a swashbuckler style. You gain the panache class feature, and can gain panache in all the ways a swashbuckler of your style can. You become trained in Acrobatics or the skill associated with your style. You also become trained in swashbuckler class DC. You don't gain any other effects of your chosen style.", GetSwashArchetypeSubclasses().ToList()).WithDemandsAbility14(Ability.Dexterity).WithDemandsAbility14(Ability.Charisma)
        .WithOnCreature(delegate (Creature swash)
        {
            swash.AddQEffect(AddSwash.PanacheGranter());
        });

    public static Feat FinishingPrecision = new TrueFeat(ModManager.RegisterFeatName("FinishingPrecision", "Finishing Precision"), 4, "You've learned how to land daring blows when you have panache.", "You gain the precise strike class feature, but you only deal 1 additional damage on a hit and 1d6 additional damage on a finisher. This damage doesn't increase as you gain levels. In addition, you gain the Basic Finisher action.", Array.Empty<Trait>(), null)
        .WithAvailableAsArchetypeFeat(AddSwash.SwashTrait)
        .WithRulesBlockForCombatAction(delegate (Creature swash)
        {
            CombatAction exampleFinisher = CombatAction.CreateSimple(swash, "Basic Finisher", new Trait[] { AddSwash.Finisher }).WithActionCost(1);
            exampleFinisher.Description = "You make a graceful, deadly attack. Make a Strike; if you hit and your weapon qualifies for precise strike, you deal the full 1d6 damage from precise strike.";
            return exampleFinisher;
        })
        .WithOnSheet(delegate (CalculatedCharacterSheetValues sheet)
        {
            sheet.AddFeat(AddSwash.PreciseStrike!, null);
        })
        .WithPermanentQEffect(null, delegate (QEffect qf)
        {
            qf.ProvideStrikeModifier = delegate (Item item)
            {
                StrikeModifiers basic = new StrikeModifiers();
                bool flag = !item.HasTrait(Trait.Ranged) && (item.HasTrait(Trait.Agile) || item.HasTrait(Trait.Finesse));
                bool flag2 = qf.Owner.HasEffect(AddSwash.PanacheId);
                if (flag && flag2)
                {
                    CombatAction basicFinisher = qf.Owner.CreateStrike(item);
                    basicFinisher.Name = "Basic Finisher";
                    basicFinisher.Illustration = new SideBySideIllustration(item.Illustration, IllustrationName.StarHit);
                    basicFinisher.Description = StrikeRules.CreateBasicStrikeDescription2(basicFinisher.StrikeModifiers, null, null, null, null, "You lose panache, whether the attack succeeds or fails.");
                    basicFinisher.ActionCost = 1;
                    basicFinisher.StrikeModifiers.OnEachTarget = async delegate (Creature owner, Creature victim, CheckResult result)
                    {
                        AddSwash.FinisherExhaustion(owner);
                    };
                    basicFinisher.Traits.Add(AddSwash.Finisher);
                    return basicFinisher;
                }
                else return null;
            };
        });

    public static Feat SwashbucklersRiposte = new TrueFeat(ModManager.RegisterFeatName("SwashbucklersRiposte", "Swashbuckler's Riposte"), 6, "You've learned to riposte against ill-conceived attacks.", "You gain the Opportune Riposte reaction.", Array.Empty<Trait>()).WithAvailableAsArchetypeFeat(AddSwash.SwashTrait)
        .WithOnSheet(delegate (CalculatedCharacterSheetValues sheet)
        {
            sheet.AddFeat(AddSwash.OpportuneRiposte!, null);
        });

    public static Feat SwashbucklersSpeed = new TrueFeat(ModManager.RegisterFeatName("SwashbucklersSpeed", "Swashbuckler's Speed"), 8, "You move faster, with or without panache.", "Increase the status bonus to your Speeds when you have panache to a +10-foot status bonus; you also gain a +5-foot status bonus to your Speeds when you don't have panache.", Array.Empty<Trait>()).WithAvailableAsArchetypeFeat(AddSwash.SwashTrait)
        .WithPermanentQEffect(delegate (QEffect qf)
        {
            qf.BonusToAllSpeeds = delegate (QEffect qf2)
            {
                return new Bonus(1, BonusType.Status, "Swashbuckler's Speed");
            };
            qf.YouAcquireQEffect = delegate (QEffect qfThis, QEffect qfGet)
            {
                if (qfGet.Id == AddSwash.PanacheId)
                {
                    QEffect qfNew = qfGet;
                    qfNew.BonusToAllSpeeds = delegate (QEffect qf)
                    {
                        return new Bonus(2, BonusType.Status, "Panache");
                    };
                    return qfNew;
                }
                else return qfGet;
            };
        });

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
    }
}
