/// <summary>
/// Driver HFSM pemain (C# murni, bukan MonoBehaviour).
/// Menyimpan state aktif dan menjalankan siklus Enter/Exit saat transisi
/// serta Tick saat state aktif.
///
/// Driver ini tidak tahu aturan transisi; keputusan transisi pada Fase 1
/// dipusatkan di PlayerStateController (centralized-transition HFSM). Ini
/// menjaga fondasi tetap sederhana dan mudah dijelaskan.
/// </summary>
public class PlayerStateMachine
{
    public IPlayerState CurrentState { get; private set; }

    /// <summary>Pindah ke state baru. Mengabaikan jika sama dengan state aktif.</summary>
    public void ChangeState(IPlayerState next)
    {
        if (next == null || next == CurrentState)
            return;

        CurrentState?.Exit();
        CurrentState = next;
        CurrentState.Enter();
    }

    /// <summary>Menjalankan Tick state aktif. Dipanggil tiap frame oleh controller.</summary>
    public void Tick()
    {
        CurrentState?.Tick();
    }
}
