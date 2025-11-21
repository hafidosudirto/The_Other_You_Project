using UnityEngine;

// Interface ini menjadi kontrak untuk semua skill.
// Setiap skill wajib memiliki fungsi TriggerSkill.
// Menggunakan interface membuat SkillBase tidak perlu tahu jenis skill yang dipasang.
// Slot skill menjadi fleksibel. Bisa Sword, Bow, Gauntlet, atau skill lain di masa depan.
//
// Interface dipilih karena memberikan struktur yang jelas.
// Semua skill akan seragam dan mudah dipanggil.
// Sistem ini menjaga SkillBase tetap sederhana walaupun jumlah skill bertambah.

public interface ISkill
{
    public void TriggerSkill(int slotIndex);

}
