/// <summary>
/// Kontrak dasar untuk semua state pemain dalam HFSM.
/// Setiap state memiliki tiga fase siklus hidup: Enter (saat masuk),
/// Tick (saat aktif, dipanggil tiap frame), dan Exit (saat keluar).
/// Name dipakai untuk debug, HUD, dan dokumentasi akademik.
///
/// Pada Fase 1, state bersifat read-only/observasional: state hanya
/// merepresentasikan kondisi pemain, belum mengubah behavior gameplay.
/// </summary>
public interface IPlayerState
{
    string Name { get; }

    void Enter();
    void Tick();
    void Exit();
}
