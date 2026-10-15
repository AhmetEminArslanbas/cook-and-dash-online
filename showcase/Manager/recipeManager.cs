using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class recipeManager : MonoBehaviour
{
    public static recipeManager Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    public recipe changeType(itemType item, bool isFull)
    {
        switch (item)
        {
            case itemType.MEAT:
                return recipe.MEAT_FOOD;
            case itemType.COOKED_MEAT:
                return recipe.COOKED_MEAT_FOOD;
            case itemType.BURNT_MEAT:
                return recipe.BURNT_MEAT_FOOD;

            case itemType.MEATBALL:
                if (isFull)
                    return recipe.BREAD_MEATBALL_FOOD;
                else
                    return recipe.HALF_BREAD_MEATBALL_FOOD;
            case itemType.COOKED_MEATBALL:
                if (isFull)
                    return recipe.BREAD_COOKED_MEATBALL_FOOD;
                else
                    return recipe.HALF_BREAD_COOKED_MEATBALL_FOOD;
            case itemType.BURNT_MEATBALL:
                if (isFull)
                    return recipe.BREAD_BURNT_MEATBALL_FOOD;
                else
                    return recipe.HALF_BREAD_BURNT_MEATBALL_FOOD;

        }    

        return recipe.NONE;
    }

    public itemType changeType(recipe item)
    {
        switch (item)
        {
            case recipe.MEAT_FOOD:
                return itemType.MEAT;
            case recipe.COOKED_MEAT_FOOD:
                return itemType.COOKED_MEAT;
            case recipe.BURNT_MEAT_FOOD:
                return itemType.BURNT_MEAT;

            case recipe.BREAD_MEATBALL_FOOD:
                return itemType.MEATBALL;
            case recipe.BREAD_COOKED_MEATBALL_FOOD:
                return itemType.COOKED_MEATBALL;
            case recipe.BREAD_BURNT_MEATBALL_FOOD:
                return itemType.BURNT_MEATBALL;

            // Marullu varyantlar eksikti: marullu bir hamburger itemType.NONE'a
            // dusuyordu. Cekirdek malzeme marulsuz haliyle ayni.
            case recipe.BREAD_MEATBALL_LETTUCE_FOOD:
                return itemType.MEATBALL;
            case recipe.BREAD_COOKED_MEATBALL_LETTUCE_FOOD:
                return itemType.COOKED_MEATBALL;
            case recipe.BREAD_BURNT_MEATBALL_LETTUCE_FOOD:
                return itemType.BURNT_MEATBALL;
        }

        return itemType.NONE;
    }

    public recipe getFlipRecipe(recipe recipe)
    {
        switch (recipe)
        {
            case recipe.HALF_BREAD_MEATBALL_FOOD:
                return recipe.BREAD_MEATBALL_FOOD;
            case recipe.HALF_BREAD_COOKED_MEATBALL_FOOD:
                return recipe.BREAD_COOKED_MEATBALL_FOOD;
            case recipe.HALF_BREAD_BURNT_MEATBALL_FOOD:
                return recipe.BREAD_BURNT_MEATBALL_FOOD;

            case recipe.HALF_BREAD_MEATBALL_LETTUCE_FOOD:
                return recipe.BREAD_MEATBALL_LETTUCE_FOOD;
            case recipe.HALF_BREAD_COOKED_MEATBALL_LETTUCE_FOOD:
                return recipe.BREAD_COOKED_MEATBALL_LETTUCE_FOOD;
            case recipe.HALF_BREAD_BURNT_MEATBALL_LETTUCE_FOOD:
                return recipe.BREAD_BURNT_MEATBALL_LETTUCE_FOOD;
        }

        // Kapatilamayan bir tarif geldiginde eskiden recipe.NONE donuyordu ve cagiran
        // taraf bu degeri yemege yaziyordu; yani zaten kapali bir hamburgere ikinci kez
        // ust ekmek konunca yemek yok oluyor ve teslim edilemiyordu. setLettuceRecipe'te
        // ayni hata daha once duzeltildi. Artik tarif DEGISMEDEN geri doner.
        return recipe;
    }

    /// <summary>
    /// Yemege marul ekler. Marul yalnizca ustu acik (HALF_BREAD_*) hamburgere eklenebilir;
    /// tasarim geregi sira "alt ekmek + kofte -> marul -> ust kapak" seklindedir ve kapagi
    /// kapatma isini getFlipRecipe yapar.
    ///
    /// Onemli: eklenemeyen durumda eskiden recipe.NONE donuyordu ve cagiran taraf bu degeri
    /// yemege yaziyordu. Yani kapagi kapanmis dogru bir hamburgere marul konursa tarif yok
    /// oluyor, yemek gorsel olarak bozuluyor ve masaya teslim edilemiyordu (table.putItem
    /// recipe.NONE'i reddediyor). Artik eklenemeyen durumda tarif DEGISMEDEN geri doner,
    /// boylece hazir yemek bozulmaz.
    /// </summary>
    public recipe setLettuceRecipe(recipe recipe)
    {
        switch (recipe)
        {
            case recipe.HALF_BREAD_MEATBALL_FOOD:
                return recipe.HALF_BREAD_MEATBALL_LETTUCE_FOOD;
            case recipe.HALF_BREAD_COOKED_MEATBALL_FOOD:
                return recipe.HALF_BREAD_COOKED_MEATBALL_LETTUCE_FOOD;
            case recipe.HALF_BREAD_BURNT_MEATBALL_FOOD:
                return recipe.HALF_BREAD_BURNT_MEATBALL_LETTUCE_FOOD;
        }

        return recipe;
    }
}
