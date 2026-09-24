using System;
using System.Collections.Generic;
using Verse;

namespace RimPersonaDirector
{
    public static class DirectorContextTracker
    {
        // 确保使用 ThreadStatic，防止后台生成任务干扰前台 UI 渲染
        [ThreadStatic]
        private static List<Pawn> _currentPawns;

        public static void SetPawns(List<Pawn> pawns)
        {
            _currentPawns = pawns;
        }

        public static List<Pawn> GetPawns()
        {
            return _currentPawns;
        }

        public static void Clear()
        {
            _currentPawns = null;
        }
    }
}