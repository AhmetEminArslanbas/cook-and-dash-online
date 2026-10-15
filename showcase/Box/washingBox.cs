using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class washingBox : NetworkBehaviour, ICraft, Iwork
{
    [SerializeField] List<objectType> dishItems = new List<objectType>();
    [SerializeField] itemType nowDishItem;

    [SerializeField] ParticleSystem washingEffect;

    GameObject canvasObj;
    Image washingImage;

    GameObject player;

    [SerializeField] float washingTime, nowTime;

    /// <summary>Yikama suresi merkezi dengeden alinir (bkz. gameBalance).</summary>
    void applyBalance()
    {
        washingTime = gameBalance.YIKAMA;
    }

    bool isWork;

    private void Awake()
    {
        applyBalance();

        canvasObj = transform.GetChild(0).gameObject;
        washingImage = canvasObj.transform.GetChild(1).GetComponent<Image>();

        canvasObj.GetComponent<Canvas>().worldCamera = Camera.main;
        dunyaGostergesi.netlestir(canvasObj);
        canvasObj.SetActive(false);

        nowDishItem = itemType.NONE;

        isWork = false;

        washingEffect.Stop();
    }

    void LateUpdate()
    {
        dunyaGostergesi.kamerayaCevir(canvasObj);
        sfx.dongu(this, sfxType.WASH, canvasObj.activeSelf && (isWork || canvasRequested));
    }

    /// <summary>Bolum biter bitmez lavaboyu bos haline getirir (sunucuda); bkz. tableManager.bolumSonuTemizligi.</summary>
    public void bolumSonuSifirla()
    {
        if (IsServer && IsSpawned)
            sifirlaClientRpc();
    }

    [ClientRpc]
    void sifirlaClientRpc()
    {
        foreach (objectType dishItem in dishItems)
            if (dishItem.itemObj != null)
                dishItem.itemObj.SetActive(false);

        nowDishItem = itemType.NONE;

        if (isWork && player != null && player.TryGetComponent(out playerMovement hareket))
            hareket.isWalk = true;

        isWork = false;
        canvasRequested = false;
        player = null;
        canvasObj.SetActive(false);

        if (washingEffect != null)
            washingEffect.Stop();
    }

    internal itemType testDishItem() { return nowDishItem; }
    internal bool testGostergeAcik() { return canvasObj.activeSelf; }

    public itemType getItem(NetworkObjectReference networkObj, bool isAsk)
    {
        if (!isAsk)
        {
            itemType rItem = nowDishItem;
            leaveItem();
            return rItem;
        }
        else
            return itemType.NONE;
    }

    public bool putItem(itemType item, GameObject obj)
    {
        if (nowDishItem == itemType.NONE && (item == itemType.DISH || item == itemType.DIRTY_DISH))
        {
            setEnableObjServerRpc(GetComponent<NetworkObject>(), item);
            return true;
        }
        else
            return false;
    }
    [ServerRpc(RequireOwnership = false)]
    void setEnableObjServerRpc(NetworkObjectReference refObj, itemType item)
    {
        setEnableObjClientRpc(refObj, item);
    }
    [ClientRpc]
    void setEnableObjClientRpc(NetworkObjectReference refObj, itemType item)
    {
        if(refObj.TryGet(out var netwokrObj))
        {
            washingBox washingBox = netwokrObj.gameObject.GetComponent<washingBox>();

            washingBox.nowDishItem = item;
            foreach (objectType dishItem in washingBox.dishItems)
            {
                if (dishItem.itemType == item)
                    dishItem.itemObj.SetActive(true);
                else
                    dishItem.itemObj.SetActive(false);
            }
        }
    }
    public void work(bool isWork, GameObject player)
    {
        if (nowDishItem == itemType.DIRTY_DISH)
        {
            if (isWork)
            {
                nowTime = 0;
                this.isWork = true;
                this.player = player;
                player.GetComponent<playerMovement>().isWalk = false;

                // YEREL GERI BILDIRIM: gosterge ve efekt sunucuyu beklemeden baslar
                // (bkz. standBox'ta ayni gerekce). RPC diger oyuncular icindir.
                if (canvasObj != null)
                    canvasObj.SetActive(true);
                if (washingEffect != null)
                    washingEffect.Play();

                isWorServerRpc(GetComponent<NetworkObject>(), true, false);
            }
            else
            {
                // Iptal karari YERELDIR ve hemen uygulanir.
                //
                // Eskiden yalnizca RPC turu ile uygulaniyordu: once nowTime = -1
                // yazilir, sonraki karede ikinci bir RPC gidip isWork = false yapardi.
                // Gecikme varken onceki birakmanin RPC'si yeni basisin ORTASINA dusup
                // yikamayi durduruyordu; oyuncu tekrar basmak zorunda kaliyordu.
                this.isWork = false;
                this.player = null;
                player.GetComponent<playerMovement>().isWalk = true;

                isWorServerRpc(GetComponent<NetworkObject>(), false, true);
            }
        }
    }
    [ServerRpc(RequireOwnership = false)]
    void isWorServerRpc(NetworkObjectReference refObj, bool isEnable, bool isCanvas)
    {
        isWorClientRpc(refObj, isEnable, isCanvas);
    }
    [ClientRpc]
    void isWorClientRpc(NetworkObjectReference refObj, bool isEnable, bool isCanvas)
    {
        if(refObj.TryGet(out var networkObj))
        {
            washingBox washingBox = networkObj.gameObject.GetComponent<washingBox>();

            // BU RPC YALNIZCA GORSELDIR; is durumuna (isWork) DOKUNMAZ.
            // Gerekce standBox ile ayni: gecikmede onceki birakmanin RPC'si yeni basisin
            // ortasina dusup isi durduruyordu. Is durumu basan oyuncunun makinesinde
            // yonetilir; RPC diger oyuncularin gostergesini senkronlar.
            if (!isCanvas && isEnable)
            {
                washingBox.canvasRequested = true;
                washingBox.canvasObj.SetActive(true);
                if (washingBox.washingEffect != null)
                    washingBox.washingEffect.Play();
            }
            else if (isCanvas)
            {
                washingBox.canvasRequested = false;
                washingBox.canvasObj.SetActive(false);
                if (washingBox.washingEffect != null)
                    washingBox.washingEffect.Stop();
            }
            
        }
    }

    // ---- Her frame RPC gonderimini onleyen durum ----
    //
    // Ilerleme cubugu her karede (60 RPC/saniye), "iptal" ve "yikandi" RPC'leri de
    // durum sunucudan geri donene kadar her karede gonderiliyordu. Reliable RPC'ler
    // transport kuyrugunda birikip uzaktaki oyuncunun baglantisini kopariyordu.
    const float IMAGE_SEND_INTERVAL = 0.1f;
    float lastImageSendTime;

    bool sentCancel;
    bool sentFinish;

    void sendImageValue(float value)
    {
        if (value < 1f && Time.unscaledTime - lastImageSendTime < IMAGE_SEND_INTERVAL)
            return;

        lastImageSendTime = Time.unscaledTime;

        // Yerel cubuk sunucuyu beklemez; RPC digerleri icindir.
        setFillTarget(value);

        setImageServerRpc(GetComponent<NetworkObject>(), value);
    }

    /// <summary>RPC ile gelen gorunurluk istegi (bkz. standBox).</summary>
    bool canvasRequested;

    /// <summary>KORUMA: is yoksa ve gosterge istenmiyorsa acik kalmamali.</summary>
    void enforceCanvasClosed()
    {
        if (!isWork && !canvasRequested && canvasObj != null && canvasObj.activeSelf)
            canvasObj.SetActive(false);
    }

    /// <summary>Test kancasi: gosterge korumasi calisiyor mu (bkz. standBox).</summary>
    internal bool testCanvasGuard()
    {
        if (canvasObj == null)
            return true;

        bool oncekiIsWork = isWork;
        bool oncekiIstek = canvasRequested;

        isWork = false;
        canvasRequested = false;
        canvasObj.SetActive(true);
        enforceCanvasClosed();

        bool kapandi = !canvasObj.activeSelf;

        isWork = oncekiIsWork;
        canvasRequested = oncekiIstek;
        return kapandi;
    }

    private void Update()
    {
        // Yumusatma herkeste calismali: istemcide isWork false olsa bile cubuk akar.
        smoothFill();
        enforceCanvasClosed();

        if (isWork)
        {
            nowTime += Time.deltaTime;
            if (nowTime < 0) //y�kama iptal
            {
                if (!sentCancel)
                {
                    sentCancel = true;
                    isWorServerRpc(GetComponent<NetworkObject>(), false, false);
                }
            }
            else if (nowTime < washingTime) //y�kan�yor
            {
                // Calisma suresi boyunca tamamlanma bayraklari bosa cikar.
                sentCancel = false;
                sentFinish = false;

                sendImageValue(nowTime / washingTime);
            }
            else
            {
                if (nowDishItem == itemType.DIRTY_DISH && !sentFinish) //y�kand�
                {
                    sentFinish = true;
                    setDishServerRpc(GetComponent<NetworkObject>());
                }
            }
        }
    }
    // Dolgu orani kaybolabilir veri: bir kare atlanmasi gorunmez, sonucu setDish
    // belirliyor. Unreliable gonderilir, boylece kuyrukta beklemez.
    [ServerRpc(RequireOwnership = false, Delivery = RpcDelivery.Unreliable)]
    void setImageServerRpc(NetworkObjectReference refObj, float value)
    {
        setImageClientRpc(refObj, value);
    }
    [ClientRpc(Delivery = RpcDelivery.Unreliable)]
    void setImageClientRpc(NetworkObjectReference refObj, float value)
    {
        if(refObj.TryGet(out var networkObj))
        {
            washingBox washingBox = networkObj.gameObject.GetComponent<washingBox>();

            washingBox.setFillTarget(value);
        }
    }

    // ---- Dolum cubugu yumusatma ----
    // Ag uzerinden saniyede ~10 guncelleme gonderiliyor; dogrudan yazilinca cubuk
    // atlaya atlaya ilerliyordu. Gelen deger hedef olarak saklanir ve her karede
    // hedefe dogru gidilir. Hiz isin gercek hiziyla ayni, ag trafigi hic artmaz.
    float targetFill = -1f;

    void setFillTarget(float value)
    {
        if (washingImage == null)
            return;

        // Geriye gidis yeni bir isin baslamasidir; orada yumusatma istemiyoruz.
        if (value < washingImage.fillAmount)
            washingImage.fillAmount = value;

        targetFill = value;
    }

    void smoothFill()
    {
        if (targetFill < 0f || washingImage == null)
            return;

        float speed = (washingTime > 0f ? 1f / washingTime : 1f) * 1.5f;
        washingImage.fillAmount = Mathf.MoveTowards(washingImage.fillAmount, targetFill, Time.deltaTime * speed);
    }

    [ServerRpc(RequireOwnership = false)]
    void setDishServerRpc(NetworkObjectReference refObj)
    {
        setDishClientRpc(refObj);
    }
    [ClientRpc]
    void setDishClientRpc(NetworkObjectReference refObj)
    {
        if(refObj.TryGet(out var networkObj))
        {
            washingBox washingBox = networkObj.gameObject.GetComponent<washingBox>();

            washingBox.nowDishItem = itemType.DISH;
            foreach (objectType dishItem in washingBox.dishItems)
            {
                if (dishItem.itemType == washingBox.nowDishItem)
                {
                    dishItem.itemObj.SetActive(true);
                }
                else
                    dishItem.itemObj.SetActive(false);
            }
            washingBox.canvasObj.SetActive(false);
            washingBox.isWork = false;
            washingBox.canvasRequested = false;
            sfx.play(sfxType.DONE);

            if(player != null)
            {
                player.GetComponent<playerMovement>().isWalk = true;
                player = null;
            }
            washingBox.washingEffect.Stop();
        }
    }



    void leaveItem()
    {
        leaveItemServerRpc(GetComponent<NetworkObject>());
    }
    [ServerRpc(RequireOwnership = false)]
    void leaveItemServerRpc(NetworkObjectReference refObj)
    {
        leaveItemClientRpc(refObj);
    }
    [ClientRpc]
    void leaveItemClientRpc(NetworkObjectReference refObj)
    {
        if(refObj.TryGet(out var netwokrObj))
        {
            washingBox washingBox = netwokrObj.gameObject.GetComponent<washingBox>();

            foreach (objectType objectType in washingBox.dishItems)
            {
                if (objectType.itemObj != null)
                    objectType.itemObj.SetActive(false);
            }
            washingBox.nowDishItem = itemType.NONE;
        }
    }
}
