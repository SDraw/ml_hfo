#pragma once
class CGestureMatcher
{
    static float NormalizeRange(float f_val, float f_min, float f_max);
public:
    // Resizes vector to 5 elements and fills with fingers stretches
    static void GetFingersStretches(const LEAP_HAND *f_hand, std::vector<float> &f_bends, std::vector<float> &f_spreads);
};
