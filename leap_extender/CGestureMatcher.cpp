#include "stdafx.h"

#include "CGestureMatcher.h"

const float g_pi = glm::pi<float>();
const float g_piQuarter = g_pi * 0.25f;
const float g_piEighth = g_pi * 0.125f;
const glm::vec3 g_zeroVector3(0.f);
const glm::vec3 g_axisX(1.f, 0.f, 0.f);
const glm::vec3 g_axisXN(-1.f, 0.f, 0.f);
const glm::vec3 g_axisZN(0.f, 0.f, 1.f);

void CGestureMatcher::GetFingersStretches(const LEAP_HAND *f_hand, std::vector<float> &f_bends, std::vector<float> &f_spreads)
{
    f_bends.resize(5U, 0.f);

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

        f_bends[i] = NormalizeRange(l_fingerBends[i], (i == 0U) ? 0.f : g_piEighth, (i == 0U) ? g_piQuarter : g_pi);
    }

    f_spreads.resize(5U, 0.f);
    const LEAP_QUATERNION &l_palmOrientation = f_hand->palm.orientation;
    const glm::quat l_palmRotation(l_palmOrientation.w, l_palmOrientation.x, l_palmOrientation.y, l_palmOrientation.z);
    const glm::vec3 l_sideDir = l_palmRotation * ((f_hand->type == eLeapHandType::eLeapHandType_Left) ? g_axisX : g_axisXN);
    const LEAP_VECTOR &l_palmPosition = f_hand->palm.position;
    const glm::vec3 l_handPos(l_palmPosition.x, l_palmPosition.y, l_palmPosition.z);

    for(size_t i = 0U; i < 5U; i++)
    {
        const LEAP_BONE &l_bone = f_hand->digits[i].proximal;
        glm::vec3 l_boneDir(l_bone.next_joint.x - l_bone.prev_joint.x, l_bone.next_joint.y - l_bone.prev_joint.y, l_bone.next_joint.z - l_bone.prev_joint.z);
        l_boneDir = glm::normalize(l_boneDir);

        if(i != 0) f_spreads[i] = glm::dot(l_boneDir, l_sideDir)*2.f;
        else
        {
            const glm::vec3 l_boneNext(l_bone.next_joint.x, l_bone.next_joint.y, l_bone.next_joint.z);
            f_spreads[i] = 1.f - NormalizeRange(glm::distance(l_boneNext, l_handPos), 40.f, 70.f);
        }

        switch(i)
        {
            case 1:
                f_spreads[i] += 0.25f;
                break;
            case 2:
                f_spreads[i] = f_spreads[i] * 2.5f + 0.5f;
                break;
            case 3:
                f_spreads[i] = -f_spreads[i] * 3.f - 0.75f;
                break;
            case 4:
                f_spreads[i] = -f_spreads[i] - 0.15f;
                break;
        }

        if(f_bends[i] > 0.5f) f_spreads[i] = glm::mix(f_spreads[i], 0.f, (f_bends[i] - 0.5f) * 2.f);
    }
}

float CGestureMatcher::NormalizeRange(float f_val, float f_min, float f_max)
{
    const float l_mapped = (f_val - f_min) / (f_max - f_min);
    return glm::clamp(l_mapped, 0.f, 1.f);
}
