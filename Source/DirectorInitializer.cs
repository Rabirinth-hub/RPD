using Verse;

namespace RimPersonaDirector
{
    public class DirectorInitializer : GameComponent
    {
        public DirectorInitializer(Game game) { }

        public override void FinalizeInit()
        {
            base.FinalizeInit();

            // 2. 执行数据迁移 (Chattiness 2.0 -> 1.0)
            DirectorMod.Settings.MigrateChattinessValuesIfNeeded();

        }
    }
}
