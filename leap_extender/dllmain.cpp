#include "stdafx.h"

#include "CLeapPoller.h"
#include "CGestureMatcher.h"

BOOL APIENTRY DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID /* lpReserved */)
{
    return TRUE;
}

CLeapPoller *g_leapPoller = nullptr;

extern "C" __declspec(dllexport) bool LeapInitialize()
{
    bool l_result = false;
    if(!g_leapPoller)
    {
        g_leapPoller = new CLeapPoller();
        l_result = g_leapPoller->Initialize();
    }
    return l_result;
}

extern "C" __declspec(dllexport) bool LeapTerminate()
{
    bool l_result = false;
    if(g_leapPoller)
    {
        g_leapPoller->Terminate();
        delete g_leapPoller;
        g_leapPoller = nullptr;
    }
    return (g_leapPoller != nullptr);
}

// f_fingersBends - array of 10 floats
// f_fingersSpreads - array of 10 floats
// f_handsDetection - array of 2 booleans
// f_handsPositions - array of 6 floats
// f_handsRotations - array of 8 floats
extern "C" __declspec(dllexport) bool LeapGetHandsData(float *f_fingersBends, float *f_fingersSpreads, bool *f_handsDetection, float *f_handsPositions, float *f_handsRotations)
{
    if(g_leapPoller)
    {
        for(size_t i = 0U; i < 10U; i++)
        {
            f_fingersBends[i] = 0.f;
            f_fingersSpreads[i] = 0.f;
        }
        for(size_t i = 0U; i < 2U; i++) f_handsDetection[i] = false;

        g_leapPoller->Update();
        const LEAP_TRACKING_EVENT *l_frame = g_leapPoller->GetFrame();
        if(l_frame)
        {
            LEAP_HAND *l_hands[2U] = { nullptr };
            for(size_t i = 0U; i < l_frame->nHands; i++)
            {
                const size_t l_handType = l_frame->pHands[i].type;
                if(!l_hands[l_handType]) l_hands[l_handType] = &l_frame->pHands[i];
            }

            for(size_t i = 0U; i < 2U; i++)
            {
                if(l_hands[i])
                {
                    std::vector<float> l_bends;
                    std::vector<float> l_spreads;
                    CGestureMatcher::GetFingersStretches(l_hands[i], l_bends, l_spreads);
                    for(size_t j = 0U; j < 5U; j++)
                    {
                        f_fingersBends[i * 5 + j] = l_bends[j];
                        f_fingersSpreads[i * 5 + j] = l_spreads[j];
                    }

                    const LEAP_VECTOR &l_position = l_hands[i]->palm.position;
                    f_handsPositions[i * 3] = l_position.x;
                    f_handsPositions[i * 3 + 1] = l_position.y;
                    f_handsPositions[i * 3 + 2] = l_position.z;

                    const LEAP_QUATERNION &l_rotation = l_hands[i]->palm.orientation;
                    f_handsRotations[i * 4] = l_rotation.x;
                    f_handsRotations[i * 4 + 1] = l_rotation.y;
                    f_handsRotations[i * 4 + 2] = l_rotation.z;
                    f_handsRotations[i * 4 + 3] = l_rotation.w;

                    f_handsDetection[i] = true;
                }
            }
        }
    }
    return (g_leapPoller != nullptr);
}

extern "C" __declspec(dllexport) void LeapSetTrackingMode(int f_mode) // 0 - desktop, 1 - HMD
{
    if(g_leapPoller) g_leapPoller->SetPolicy((f_mode == 0) ? 0U : eLeapPolicyFlag::eLeapPolicyFlag_OptimizeHMD, (f_mode == 0) ? eLeapPolicyFlag::eLeapPolicyFlag_OptimizeHMD : 0U);
}
