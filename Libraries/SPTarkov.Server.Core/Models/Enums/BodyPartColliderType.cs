﻿using SPTarkov.Server.Core.Utils.Json.Converters;

namespace SPTarkov.Server.Core.Models.Enums;

[EftEnumConverter]
public enum BodyPartColliderType
{
    None = -1,
    HeadCommon,
    RibcageUp,
    Pelvis = 3,
    LeftUpperArm,
    LeftForearm,
    RightUpperArm,
    RightForearm,
    LeftThigh,
    LeftCalf,
    RightThigh,
    RightCalf,
    ParietalHead,
    BackHead,
    Ears,
    Eyes,
    Jaw,
    NeckFront,
    NeckBack,
    RightSideChestUp,
    LeftSideChestUp,
    SpineTop,
    SpineDown,
    PelvisBack,
    RightSideChestDown,
    LeftSideChestDown,
    RibcageLow,
}
