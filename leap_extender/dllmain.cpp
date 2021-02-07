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

extern "C" __declspec(dllexport) bool LeapGetHandsData(float *f_fingers, bool *f_hands) // Array of 10 floats, array of 2 booleans
{
    if(g_leapPoller)
    {
        for(size_t i = 0U; i < 10U; i++) f_fingers[i] = 0.f;
        for(size_t i = 0U; i < 2U; i++) f_hands[i] = false;

        g_leapPoller->Update();
        const LEAP_TRACKING_EVENT *l_frame = g_leapPoller->GetFrame();
        if(l_frame)
        {
            LEAP_HAND *l_hands[2U] = { nullptr };
            for(size_t i = 0U; i < l_frame->nHands; i++)
            {
                if(!l_hands[l_frame->pHands[i].type]) l_hands[l_frame->pHands[i].type] = &l_frame->pHands[i];
            }

            for(size_t i = 0U; i < 2U; i++)
            {
                if(l_hands[i])
                {
                    std::vector<float> l_stretches;
                    CGestureMatcher::GetFingersStretches(l_hands[i], l_stretches);
                    for(size_t j = 0U; j < 5U; j++) f_fingers[i * 5 + j] = l_stretches[j];

                    f_hands[i] = true;
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
