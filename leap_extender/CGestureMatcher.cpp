#include "stdafx.h"

#include "CGestureMatcher.h"

const float g_pi = glm::pi<float>();
const float g_piQuarter = g_pi * 0.25f;
const glm::vec3 g_zeroVector3(0.f);

void CGestureMatcher::GetFingersStretches(const LEAP_HAND *f_hand, std::vector<float> &f_result)
{
    f_result.resize(5U, 0.f);

    float l_fingerBends[5U] = { 0.f };
    for(size_t i = 0U; i < 5U; i++)
    {
        const LEAP_DIGIT &l_finger = f_hand->digits[i];
        const size_t l_startBoneIndex = ((i == 0U) ? 1U : 0U);
        glm::vec3 l_prevDirection(0.f);
        for(size_t j = l_startBoneIndex; j < 4U; j++)
        {
            const LEAP_BONE &l_bone = l_finger.bones[j];
            glm::vec3 l_direction(l_bone.next_joint.x - l_bone.prev_joint.x, l_bone.next_joint.y - l_bone.prev_joint.y, l_bone.next_joint.z - l_bone.prev_joint.z);
            l_direction = glm::normalize(l_direction);
            if(j > l_startBoneIndex) l_fingerBends[i] += glm::acos(glm::dot(l_direction, l_prevDirection));
            l_prevDirection = l_direction;
        }

        f_result[i] = NormalizeRange(l_fingerBends[i], (i == 0U) ? 0.f : g_piQuarter, (i == 0U) ? g_piQuarter : g_pi);
    }
}

float CGestureMatcher::NormalizeRange(float f_val, float f_min, float f_max)
{
    const float l_mapped = (f_val - f_min) / (f_max - f_min);
    return glm::clamp(l_mapped, 0.f, 1.f);
}
