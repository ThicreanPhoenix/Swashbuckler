﻿using Dawnsbury.Modding;

namespace Dawnsbury.Mods.Phoenix;
    public class BootUp
{
    [DawnsburyDaysModMainMethod]

    public static void LoadMod()
        {
        AddSwash.LoadSwash();
        AddWeapons.LoadWeapons();
        AddMulticlassSwash.LoadMulticlassSwash();
        }
    }