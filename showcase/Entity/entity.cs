using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

[System.Serializable]
public class entityImages
{
    public recipe imageRecipe;
    public GameObject image;
}
public class entity : NetworkBehaviour
{
    public List<entityImages> entityImages = new List<entityImages>();

    public GameObject entityCoinText;

    [HideInInspector] public GameObject targetTable;

    NavMeshAgent entityNavMash;
    Rigidbody rb;

    [HideInInspector] public Vector3 frontDoorV3, backDoorV3;

    string orderFood;

    bool isReady;

    bool isPos = false;

    /// <summary>Calisan kurtarma sayaci; yeni hedef verilince iptal edilir.</summary>
    Coroutine kurtarmaSayaci;

    [HideInInspector] public GameObject canvasObj;

    /// <summary>
    /// Musteri uzerindeki dunya-uzayi canvas'inin duruşu.
    ///
    /// Kamera Euler(65,0,0) ile bakiyor; bu deger canvas'i o kameraya gore dogru
    /// okunacak sekilde yerlestirir (canvas'in cocuklari kendi eksenlerinde 180
    /// derece cevrilmis oldugu icin iki ters birbirini goturur). Tek kaynak burasi:
    /// table.setEntityImageClientRpc de bunu kullanir.
    /// </summary>
    public static readonly Quaternion CANVAS_DURUSU = Quaternion.Euler(115f, 180f, 0f);

    const float TABELA_YUKSEKLIGI = 1.85f;

    /// <summary>Sahnedeki musteriler. orderHud her karede sahneyi taramasin diye tutulur.</summary>
    internal static readonly List<entity> hepsi = new List<entity>();

    void OnEnable() { hepsi.Add(this); }
    void OnDisable() { hepsi.Remove(this); }

    // ---- Kuyruk sabri (kapida bekleyen musteri) ----
    //
    // Sabir sunucuda sayilir (tableManager.kuyrukSabriniIsle); gosterge icin her cihaz
    // baslangic anini ve sureyi bilir, kalan sureyi kendi saatinden hesaplar (her karede
    // ag mesaji gitmez). Gostergeyi oyunBildirimi cizer.

    const byte KUYRUKTA = 0, OTURDU = 1, KACTI = 2;

    /// <summary>Kacan musterinin basindaki unlemin ekranda kaldigi sure.</summary>
    const float KACIS_ISARETI_SURESI = 2.5f;

    float kuyrukBaslangic = -1f, kuyrukSuresi = 1f, kacisAni = -1f;

    /// <summary>Kuyrukta kalan sabir: 1 = yeni geldi, 0 = gitmek uzere; kuyrukta degilse -1.</summary>
    internal float kuyrukKalan
    {
        get { return kuyrukBaslangic < 0f ? -1f : Mathf.Clamp01(1f - (Time.time - kuyrukBaslangic) / kuyrukSuresi); }
    }

    /// <summary>Sabri bitip giden musteri (kisa bir sure isaretlenir).</summary>
    internal bool kacisIsareti
    {
        get { return kacisAni >= 0f && Time.time - kacisAni < KACIS_ISARETI_SURESI; }
    }

    /// <summary>Sunucuda: musteri kuyruga girdi / masaya cagrildi / sabri bitip gitti.</summary>
    internal void kuyrugaGirdi(float sure) { kuyrukClientRpc(KUYRUKTA, sure); }
    internal void masayaCagrildi() { kuyrukClientRpc(OTURDU, 0f); }
    internal void sabriBitti() { kuyrukClientRpc(KACTI, 0f); }

    [ClientRpc]
    void kuyrukClientRpc(byte durum, float sure)
    {
        kuyrukBaslangic = durum == KUYRUKTA ? Time.time : -1f;
        kuyrukSuresi = Mathf.Max(0.1f, sure);

        if (durum == KUYRUKTA)
            sfx.play(sfxType.DOOR);

        if (durum == KACTI)
        {
            kacisAni = Time.time;
            sfx.play(sfxType.LOST);
        }
    }

    // ---- OYUNCU MUSTERININ ICINDEN GECER ----
    //
    // Musterinin kapsulu oyuncu icin kati bir engeldi. Kapida bekleyen ya da kapidan
    // gecen musteri kapiyi tikiyor, oyuncu mutfaga/disari gecemiyordu (11.10.2026,
    // kullanici: "kapinin onune gectiler, kapidan dahi gecemiyorum"). Musteri oyuncuyu
    // zaten itemiyor (kinematik), yani carpisma yalnizca oyuncuyu durduruyordu.

    static bool katmanAyarlandi;

    void oyunculariYokSay()
    {
        if (!katmanAyarlandi)
        {
            katmanAyarlandi = true;

            int oyuncuKatmani = LayerMask.NameToLayer("player"), musteriKatmani = LayerMask.NameToLayer("entity");
            if (oyuncuKatmani >= 0 && musteriKatmani >= 0)
                Physics.IgnoreLayerCollision(oyuncuKatmani, musteriKatmani, true);
        }

        // Katman ayari prefab'in katmanina guvenir; ayrica collider ciftleri de kapatilir.
        foreach (playerMovement oyuncu in playerMovement.hepsi)
            oyuncuyuYokSay(oyuncu);
    }

    void oyuncuyuYokSay(playerMovement oyuncu)
    {
        if (oyuncu == null)
            return;

        foreach (Collider benim in GetComponentsInChildren<Collider>())
            foreach (Collider onun in oyuncu.GetComponentsInChildren<Collider>())
                if (!benim.isTrigger && !onun.isTrigger)
                    Physics.IgnoreCollision(benim, onun, true);
    }

    /// <summary>Yeni dogan oyuncu icin: sahnedeki butun musteriler onu engel saymaz.</summary>
    internal static void yeniOyuncu(playerMovement oyuncu)
    {
        foreach (entity e in hepsi)
            e.oyuncuyuYokSay(oyuncu);
    }

    /// <summary>Musteri su an bir siparis gosteriyorsa onun ikonu, yoksa null.</summary>
    internal Sprite siparisIkonu()
    {
        GameObject aktif = aktifSiparis();
        SpriteRenderer sr = aktif != null ? aktif.GetComponent<SpriteRenderer>() : null;
        return sr != null ? sr.sprite : null;
    }

    /// <summary>Siparisin oyuncuya gosterilen kisa adi (ikon nesnesinin adindan turetilir).</summary>
    internal string siparisAdi()
    {
        GameObject aktif = aktifSiparis();
        if (aktif == null)
            return "";

        string ad = aktif.name.ToLowerInvariant();

        if (ad.Contains("lettuce")) return localization.t("order.lettuce");
        if (ad.Contains("burger")) return localization.t("order.burger");
        return localization.t("order.meat");
    }

    GameObject aktifSiparis()
    {
        if (canvasObj == null || !canvasObj.activeInHierarchy)
            return null;

        foreach (entityImages item in entityImages)
            if (item.image != null && item.image.activeSelf)
                return item.image;

        return null;
    }

    [HideInInspector] public GameObject targetChair;


    // ---- Yuruyus animasyonu ----
    //
    // Musteriler "kayarak"/"ucarak" gidiyor gibi gorunuyordu: prefab'da Animator olmasina
    // ragmen kod hicbir yerde yuruyus parametresini set etmiyordu, yani model hep bos
    // duruşta kaliyor ve NavMeshAgent onu suruklüyordu.
    //
    // Hiz, NavMeshAgent.velocity yerine KONUM FARKINDAN hesaplanir. Sebep: istemcide
    // ajan kapali (bkz. OnNetworkSpawn) ve orada velocity her zaman sifirdir; konum farki
    // ise hem sunucuda hem istemcide dogru calisir, yani animasyon her iki tarafta ayni
    // gorunur.
    Animator anim;
    bool animVar;
    Vector3 sonKonum;

    /// <summary>Bu hizin uzerinde yuruyor sayilir (kucuk senkron titremeleri elenir).</summary>
    const float YURUME_HIZ_ESIGI = 0.15f;


    void Awake()
    {
        entityNavMash = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();
        isReady = false;

        anim = GetComponent<Animator>();
        if (anim == null)
            anim = GetComponentInChildren<Animator>();

        animVar = anim != null && hasParameter(anim, "isWalk");
        sonKonum = transform.position;

        // ROOT MOTION KAPATILIR.
        //
        // Entity prefab'inda applyRootMotion acikti (oyuncuda kapali). Animator'de
        // yalnizca bos durus klibi oldugu surece bunun etkisi yoktu; yuruyus klibi
        // eklenince klip karakteri KENDI surukledi ve NavMeshAgent ile cakisti -
        // musteriler kapidan gecemez oldu. Yol bulmanin tek sahibi NavMeshAgent
        // olmali, animasyon yalnizca gorsel olmali.
        if (anim != null)
            anim.applyRootMotion = false;

        // ---- HAREKETIN TEK SAHIBI: NavMeshAgent ----
        //
        // Prefab'da Rigidbody kinematik DEGILDI ve yercekimi acikti; setNavMesh de
        // her cagrida isKinematic = false yapiyordu. Yani ayni nesneyi hem agent
        // (transform'a dogrudan yazarak) hem fizik motoru suruyordu. Bu ikisi her
        // karede birbirini eziyor: musteri titriyor, duvara sürtunurken donuyor,
        // bazen itilip yol disina cikiyordu. Oyunun "hareketler cok kotu" gorunmesinin
        // ana sebebi buydu.
        //
        // Kinematik Rigidbody trigger olaylarini ALMAYA DEVAM EDER (sandalyeye oturma
        // OnTriggerEnter ile calisiyor), yani davranis kaybi yok.
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.None;
        }

        // NOT: burada bir ara ajanin donus hizi/ivmesi/durma mesafesi degistiriliyordu.
        // Hicbiri olcume dayanmiyordu ve hareketi kullanicinin gozunde kotulestirdi;
        // prefab'daki degerler oldugu gibi birakiliyor.

        canvasObj = transform.GetChild(0).gameObject;

        // Prefab'da tabela 2.11'de (musteri boyunun ~2 kati) duruyordu. Kamera egik
        // baktigi icin ikon musteriden kopup masanin ustune kaymis gorunuyordu;
        // basin hemen ustune indirildi.
        canvasObj.transform.localPosition = new Vector3(0f, TABELA_YUKSEKLIGI, 0f);

        canvasObj.GetComponent<Canvas>().worldCamera = Camera.main;
        canvasObj.SetActive(false);

        entityCoinText.SetActive(false);

        // NOT: eskiden burada "if (IsClient) rb.isKinematic = true;" vardi.
        // Awake'te IsClient/IsServer HENUZ GECERLI DEGIL (NetworkBehaviour bunlari
        // OnNetworkSpawn'dan itibaren dogru dondurur), bu yuzden istemcilerde
        // Rigidbody cogu zaman kinematik olmuyordu. Dogru yer OnNetworkSpawn.
    }

    /// <summary>
    /// Hareketin tek sahibi SUNUCUDUR.
    ///
    /// Musteriler bazen bir anda uctuklarini ya da duz gitmeleri gerekirken
    /// takildiklarini gosteriyordu. Sebep uc kuvvetin ayni nesneyi ayni anda
    /// surmesiydi:
    ///   1) NavMeshAgent (istemcide de calisiyordu, kendi yol bulmasini uyguluyordu)
    ///   2) Rigidbody fizigi (istemcide kinematik yapilamamisti, bkz. Awake notu)
    ///   3) NetworkTransform (sunucudan gelen konumu yaziyordu)
    /// Ucu cekisince konum her karede ileri geri ziplayip ani sicramalar uretiyordu.
    ///
    /// Cozum: istemcide yol bulma ve fizik tamamen kapatilir; istemci yalnizca
    /// sunucudan gelen konumu gosterir. Bu ayni zamanda telefon icin belirgin bir
    /// kazanc: her istemci artik musteri basina NavMesh hesabi yapmiyor.
    /// </summary>
    public override void OnNetworkSpawn()
    {
        oyunculariYokSay();

        if (IsServer)
        {
            // MUSTERILER BIRBIRININ ICINDEN GECER.
            //
            // Ajanlar birbirinden kaciniyordu (prefab: en yuksek kalite). Restoranin tek
            // kapisi var ve giren ile cikan ayni kapidan geciyor: iki ajan kapida
            // karsilasinca birbirini itip duruyor, arkadan gelenler de yigiliyordu.
            // Kacinma kapaninca kimse kimseyi beklemez; kisa bir ic ice gecme, kapida
            // kilitlenen bir kuyruktan iyidir.
            if (entityNavMash != null && System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-eskimusteri") < 0)
                entityNavMash.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;

            return;
        }

        if (entityNavMash != null)
        {
            entityNavMash.updatePosition = false;
            entityNavMash.updateRotation = false;
            entityNavMash.enabled = false;
        }

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.None;
        }
    }

    // Para yazisinin baslangic konumu. Animasyon her seferinde buradan basladigi icin
    // bir kez yakalanip saklanir; aksi halde ikinci musteride yazi bir onceki
    // animasyonun bittigi yerden baslar.
    Vector3 coinTextStartPos;
    bool coinTextStartCaptured;

    /// <summary>
    /// Musteri para birakinca oynayan animasyon.
    ///
    /// Eskiden yazi 3 saniye boyunca ASAGI kayip kuculerek soluyordu; hem yavasti hem
    /// de odul yerine kaybetme hissi veriyordu. Yeni animasyon oyunlarda yerlesmis
    /// kalibi izler: sifirdan hafif tasarak belirir (pop), YUKARI suzulur ve sona
    /// dogru hizlica solar. Toplam sure 3 sn'den 1.1 sn'ye indi, boylece arka arkaya
    /// gelen musterilerde yazilar ust uste binmiyor.
    /// </summary>
    public void coinAdded()
    {
        foreach (var item in entityImages)
            item.image.SetActive(false);

        if (entityCoinText == null || canvasObj == null)
            return;

        // CANVAS DURUSU BURADA DA UYGULANIR.
        //
        // Duruş normalde siparis ikonu gosterilirken (table.setEntityImageClientRpc)
        // bir kez atanıyordu. O mesaj gelmemisse ya da musteri hic siparis
        // gostermeden para birakmissa canvas musterinin kendi acisinda kalıyor ve
        // "+5" yan yatmis/ters gorunuyordu - gorsel testte iki musteriden birinde
        // tam olarak bu goruldu. Duruş yaziyi gosteren yerde de uygulanınca tek bir
        // ag mesajina bagimlilik kalkiyor.
        canvasObj.transform.rotation = CANVAS_DURUSU;

        Transform t = entityCoinText.transform;
        TextMeshProUGUI text = entityCoinText.GetComponent<TextMeshProUGUI>();

        if (!coinTextStartCaptured)
        {
            coinTextStartPos = t.localPosition;
            coinTextStartCaptured = true;
        }

        // Yarim kalmis animasyon varsa temizlenir; ust uste binmesin.
        t.DOKill();
        if (text != null)
            DOTween.Kill(text);

        canvasObj.SetActive(true);
        entityCoinText.SetActive(true);
        sfx.play(sfxType.COIN);

        // Her seferinde ayni baslangic durumu
        t.localPosition = coinTextStartPos;
        t.localScale = Vector3.zero;
        if (text != null)
            text.alpha = 1f;

        styleCoinText(text);

        const float SURE = 1.1f;

        // Yazinin son olcegi. Sahnedeki canvas olcegi bu yaziyi oyuncunun kendisinden
        // buyuk gosteriyordu ("inanilmaz buyuk, cok buyuk efekt"); punto degerine
        // dokunmak yerine olcek burada kontrol ediliyor, cunku punto dunya-uzayi
        // canvas'ta baska ogelerle paylasiliyor.
        const float ODUL_OLCEK = 0.4f;

        // 1) Pop: sifirdan hafif tasip yerine oturur, dikkat ceker
        // Tam ad: Unity.VisualScripting'de de Sequence var, belirsizlik olmasin.
        DG.Tweening.Sequence pop = DOTween.Sequence();
        pop.Append(t.DOScale(ODUL_OLCEK * 1.2f, 0.18f).SetEase(Ease.OutBack));
        pop.Append(t.DOScale(ODUL_OLCEK, 0.12f).SetEase(Ease.OutQuad));

        // Animasyonlar musteriye BAGLANIR: musteri odeme animasyonu bitmeden yok
        // edilirse (masadan kalkip cikti) tween'ler yok olmus nesneye yazmaya calisip
        // her karede hata uretiyordu. SetLink nesne yok olunca tween'i oldurur.
        pop.SetLink(gameObject);

        // 2) Yukari suzulme: odul hissi yukari dogrudur
        //    Tabela CANVAS_DURUSU ile ters cevrili durdugu icin yerel -Y ekranda
        //    yukaridir; +Y ile yazi musterinin basindan asagi kayiyordu (karede goruldu).
        t.DOLocalMoveY(coinTextStartPos.y - 1.2f, SURE).SetEase(Ease.OutCubic).SetLink(gameObject);

        // 3) Son %45'te solma
        if (text != null)
        {
            DOTween.To(() => text.alpha, x => text.alpha = x, 0f, SURE * 0.45f)
                   .SetDelay(SURE * 0.55f)
                   .SetEase(Ease.InQuad)
                   .SetLink(gameObject)
                   .OnComplete(() =>
                   {
                       entityCoinText.SetActive(false);
                       canvasObj.SetActive(false);

                       // Bir sonraki kullanim icin durumu geri al.
                       // Olcek 1 degil ODUL_OLCEK'e donuyor: aksi halde animasyon
                       // kesintiye ugrarsa yazi tam boyda (cok buyuk) asili kalir.
                       t.localPosition = coinTextStartPos;
                       t.localScale = Vector3.one * ODUL_OLCEK;
                       text.alpha = 1f;
                   });
        }
    }

    // ---- SIPARIS IKONU / PARA YAZISI DURUSU ----
    //
    // Burada eskiden faceCanvasToCamera vardi: canvas'i her karede kameraya
    // ceviriyordu. YANLISTI ve iki hatanin da sebebiydi ("+5 ters", "siparis ikonu
    // ters ve durusu degismis").
    //
    // Sebep prefab'in kurulumunda: canvas'in cocuklari (coinText ve butun siparis
    // ikonlari) kendi eksenlerinde 180 derece cevrilmis duruyor. Kamera Euler(65,0,0)
    // ile bakiyor ve table.setEntityImageClientRpc canvas'a sabit Euler(115,180,0)
    // veriyor; bu ikisi birlikte "canvas'in arkasini gormek + icerigin 180 derece
    // cevrilmis olmasi" anlamina gelir ve iki ters birbirini goturur, yazi DUZ okunur.
    // Canvas'i kameraya cevirmek bu dengeyi bozuyor: arkadan gorme ortadan kalkiyor,
    // cocuklarin 180 derecesi ortada kaliyor ve her sey ters gorunuyor.
    //
    // Dolayisiyla dogru davranis canvas'in donusune HIC dokunmamaktir.

    /// <summary>Animator'de bu adda bir parametre var mi? Yoksa animasyon hic denenmez.</summary>
    static bool hasParameter(Animator a, string ad)
    {
        foreach (AnimatorControllerParameter p in a.parameters)
            if (p.name == ad)
                return true;

        return false;
    }

    /// <summary>
    /// Musterinin basindaki tabela her karede AYNI DUNYA DURUSUNDA tutulur.
    ///
    /// Tabela musterinin cocugu; musteri yurureken dondugu icin tabela da onunla
    /// birlikte donuyor ve yazi yan yatmis, hatta ters gorunuyordu ("hareket
    /// ederken ters felan donuyor"). Duruş bir kez ayarlamak yetmez, cunku
    /// ebeveynin donusu her karede degisiyor.
    ///
    /// Kameraya BAKTIRMAK (billboard) degil, sabit bir dunya donusu kullanilir:
    /// oyunun kamerasi sabit acili, bu yuzden sabit donus her zaman ekrana bakar
    /// ve billboard'un daha once yol actigi ayna/ters yazi sorunu geri gelmez.
    /// </summary>
    /// <summary>
    /// Otomatik test, olcumun gercekten yon olctugunu kanitlamak icin duruşu
    /// bilerek bozuyor; bu koruma acikken bozma aninda geri alindigi icin
    /// negatif kontrol olcum yapamiyor. Yalnizca test kapatir.
    /// </summary>
    internal static bool testDurusSabitlemeKapali;

    void LateUpdate()
    {
        if (testDurusSabitlemeKapali)
            return;

        if (canvasObj != null && canvasObj.activeInHierarchy)
            canvasObj.transform.rotation = CANVAS_DURUSU;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f)
            return;

        sandalyeyiIzle(dt);

        if (!animVar)
            return;

        // Sandalyeye oturduktan sonra musteri ebeveynle birlikte tasinabilir; yatay
        // hiz bakilarak dikey/parent kaynakli hareketler yuruyus sayilmaz.
        Vector3 fark = transform.position - sonKonum;
        fark.y = 0f;
        sonKonum = transform.position;

        float hiz = fark.magnitude / dt;
        bool yuruyor = !isReady && hiz > YURUME_HIZ_ESIGI;

        anim.SetBool("isWalk", yuruyor);

        // ANIMASYON HIZI OLCEKLENMEZ.
        //
        // Bir ara "adimlar hiza uysun" diye anim.speed, tahmini bir referans hiza
        // oranlaniyordu. OLCUM bunu curuttu: f_walk klibinin averageSpeed'i
        // (-0.0006, 0, 0.0009), yani duzlemde 0.001 birim/sn - klip YERINDE yuruyor,
        // kok hareketi hic yok. Boyle bir klipte "dogal hiz" diye bir referans
        // bulunmadigi icin oran da anlamsizdi; tek yaptigi animasyonu ~1.55 kat
        // hizlandirip musteriyi telasli gostermekti (kullanici: "oncekinden bile
        // kotu"). Klip kendi hizinda oynar.
    }

    /// <summary>
    /// Para yazisini "odul" gibi gosterir.
    ///
    /// Sahnedeki yazi duz beyaz, kucuk ve cerceveSizdi; oyunun uzerinde silik bir
    /// "+5" olarak duruyordu. Odul geri bildirimi okunakli ve belirgin olmali: altin
    /// sarisi dolgu, koyu kontur ve hafif govde. Metin de para simgesiyle yazilir.
    ///
    /// Stil her animasyonda yeniden uygulanir; sahne varliginin ayarina bagli kalmaz.
    /// </summary>
    static void styleCoinText(TextMeshProUGUI text)
    {
        if (text == null)
            return;

        text.color = new Color(1f, 0.84f, 0.25f);      // altin sarisi
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = false;

        // PUNTOYA DOKUNULMAZ.
        //
        // Bu yazi DUNYA UZAYINDA bir canvas'ta duruyor ve sahnedeki degeri 1.5.
        // Once "Mathf.Max(fontSize, 36)" yaziliyordu; bu, punto olcegini 24 katina
        // cikarip ekrani kaplayan devasa bir yazi uretti. Sahnedeki olcu dogru olcudur.

        // Koyu kontur: acik zeminde de okunur kalsin.
        //
        // Deger KUCUK tutulur. 0.15 denendi ve karakterleri gorunur sekilde sisirip
        // yaziyi oldugundan cok daha buyuk gosterdi; kullanici "inanilmaz buyuk, cok
        // buyuk efekt" diye bildirdi. 0.05 okunakliligi saglarken kaliligi artirmiyor.
        uiSkin.kontur(text, 0.05f, new Color32(40, 25, 0, 255));

        // Yazinin basina para simgesi eklenir ("+5" -> "+5 $")
        string ham = text.text;
        if (!string.IsNullOrEmpty(ham) && !ham.Contains("+"))
            text.text = "+" + ham;
    }

    public void setNavMesh(Vector3 pos)
    {
        // Yol bulma yalnizca sunucuda calisir (bkz. OnNetworkSpawn). Istemcide
        // cagrilirsa ajan yeniden acilir ve ag konumuyla cakisip sicramalara yol acar.
        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer)
            return;

        // AYNI HEDEF YENIDEN VERILMEZ.
        //
        // Kuyruk her saniye yeniden diziliyordu; yerinde bekleyen musteriye de her
        // saniye ayni hedef verilip yolu bastan hesaplatiliyordu (musteri oldugu yerde
        // donup kipirdiyordu).
        if (hedefVerildi && entityNavMash.enabled && (pos - verilenHedef).sqrMagnitude < 0.01f)
            return;

        verilenHedef = pos;
        hedefVerildi = true;

        entityNavMash.enabled = true;
        isPos = false;

        // HEDEF NAVMESH'E DUSURULUR.
        //
        // Olcumde musterilerin 2.6 - 4.9 birim isinlandigi goruldu; sebebi hedefin
        // ULASILAMAZ olmasiydi. Sandalyeye gonderirken hedef olarak sandalyenin kendi
        // konumu veriliyor, ama sandalye NavMesh'te bir DELIK (engel). Ajan oraya
        // asla varamiyor, en yakin noktada duruyor ve 12 saniye sonra kurtarma onu
        // isinlatiyordu - ekranda "musteri zipladi" olarak gorunen sey buydu.
        //
        // Hedef once NavMesh'e dusurulur: sandalyenin yanindaki en yakin yurunebilir
        // nokta. Musteri oraya yuruyerek varir, sandalyenin trigger'ina degdiginde de
        // oturma zaten kendiliginden tetiklenir.
        if (NavMesh.SamplePosition(pos, out NavMeshHit hedefVurus, 2.5f, NavMesh.AllAreas))
            pos = hedefVurus.position;

        entityNavMash.SetDestination(pos);
        frontDoorV3 = pos;

        // ONCEKI KURTARMA SAYACI IPTAL EDILIR.
        //
        // Her hedef degisiminde yeni bir setPos coroutine'i baslatiliyor, eskiler ise
        // calismaya devam ediyordu. Kuyruk her kaydiginda bir tane daha ekleniyor ve
        // hepsi 12. saniyede ISINLAMA deniyordu; olcumde "hedefe varamadi" loglarinin
        // cogunun 0.1 birimlik anlamsiz isinlamalar olmasinin sebebi buydu.
        if (kurtarmaSayaci != null)
            StopCoroutine(kurtarmaSayaci);

        kurtarmaSayaci = StartCoroutine(setPos()); //eger gidemediyse isinlancak sekilde ayarla
    }
    /// <summary>
    /// 12 saniyede hedefe varamayan musteri icin son cozum.
    ///
    /// Eskiden kosulsuz olarak frontDoorV3'e isinliyordu. Kuyruk noktasi haritanin
    /// disina tasabildigi icin (bkz. tableManager kuyruk slotlari) bu, musteriyi
    /// duvarin arkasina ya da NavMesh disina birakiyor ve musteri bir daha
    /// gorunmuyordu. Artik hedefin NavMesh uzerinde bir karsiligi aranir; yoksa
    /// isinlama YAPILMAZ, musteri yurumeye devam eder.
    /// </summary>
    IEnumerator setPos()
    {
        yield return new WaitForSeconds(12);
        if (isPos)
            yield break;

        // HEDEFE ZATEN YAKINSA ISINLAMA YOK.
        //
        // Olcumde goruldu: musteriler cogu zaman hedeflerinin 0.1-0.4 birim
        // yakinindayken bile isinlaniyordu (NavMeshAgent durma mesafesi yuzunden
        // "vardim" sayilmiyor). Her isinlama ekranda bir sicrama demek; kullanici
        // hareketi bu yuzden bozuk goruyor. Kurtarma yalnizca musteri gercekten
        // takildiysa (1.5 birimden uzakta) devreye girer.
        if (Vector3.Distance(transform.position, frontDoorV3) < 1.5f)
            yield break;

        // Ajan yola cikmis ve hedefine yaklasiyorsa da karisilmaz: kurtarma yalnizca
        // GERCEKTEN takilmis musteri icindir.
        if (entityNavMash != null && entityNavMash.enabled && !entityNavMash.pathPending &&
            entityNavMash.remainingDistance <= entityNavMash.stoppingDistance + 1.5f)
            yield break;

        if (NavMesh.SamplePosition(frontDoorV3, out NavMeshHit vurus, 2f, NavMesh.AllAreas))
        {
            // TESHIS: bu isinlama, gozlem modunda olculen "musteri bir karede 3.35
            // birim sicradi" bulgusunun en guclu adayi. Ne kadar ve nereden nereye
            // oldugu loga yazilir ki sebep tahmin edilmesin.
            Debug.LogWarning("[entity] ISINLAMA: " + name +
                             " hedefe 12 sn'de varamadi, " +
                             Vector3.Distance(transform.position, vurus.position).ToString("F2") +
                             " birim isinlandi (" + transform.position.ToString("F1") +
                             " -> " + vurus.position.ToString("F1") + ")");

            transform.position = vurus.position;

            // Isinlamadan sonra agent'in ic konumu eski yerde kalir; uyumlanmazsa
            // musteri oradan geri kayar.
            if (entityNavMash != null && entityNavMash.enabled)
                entityNavMash.Warp(vurus.position);
        }
        else
            Debug.LogWarning("[entity] Kuyruk noktasi NavMesh disinda, isinlama iptal: " + frontDoorV3);
    }

    Vector3 verilenHedef;
    bool hedefVerildi;

    // ---- SANDALYEYE OTURMA ----
    //
    // Oturma yalnizca fizik tetikleyicisine (OnTriggerEnter) bagliydi. Sandalye NavMesh'te
    // bir delik oldugu icin musteri yanindaki en yakin noktada durur; o nokta
    // tetikleyicinin disinda kalirsa musteri masanin yaninda SONSUZA KADAR ayakta bekler,
    // masa "dolu" kalir, bolum bitmez. Artik sunucu mesafeye de bakar: sandalyeye yeterince
    // yaklasan ya da yolunun sonuna gelip duran musteri oturur.

    /// <summary>Sandalyeye bu kadar yaklasan musteri oturur (tetikleyicinin olculen mesafesi).</summary>
    const float OTURMA_MESAFESI = 1.4f;

    /// <summary>Yolunun sonunda duran musteri sandalyeye en cok bu kadar uzaktaysa yine oturur.</summary>
    const float OTURMA_EN_UZAK = 3.5f;

    float sandalyeBekleme;

    void sandalyeyiIzle(float dt)
    {
        if (isReady || targetChair == null || !IsSpawned || !IsServer)
        {
            sandalyeBekleme = 0f;
            return;
        }

        Vector3 fark = targetChair.transform.position - transform.position;
        fark.y = 0f;
        float uzaklik = fark.magnitude;

        if (uzaklik <= OTURMA_MESAFESI)
        {
            otur();
            return;
        }

        bool durdu = entityNavMash != null && entityNavMash.enabled && entityNavMash.isOnNavMesh &&
                     !entityNavMash.pathPending && entityNavMash.remainingDistance <= 0.25f;

        sandalyeBekleme = durdu ? sandalyeBekleme + dt : 0f;

        if (sandalyeBekleme > 0.6f && uzaklik <= OTURMA_EN_UZAK)
            otur();
    }

    /// <summary>Cikisa yuruyen musteri en gec bu kadar sonra kaldirilir.</summary>
    const float CIKIS_EN_UZUN = 20f;

    public void setNavMeshBack()
    {
        hedefVerildi = false;
        targetChair = null;

        entityNavMash.enabled = true;
        entityNavMash.SetDestination(backDoorV3);

        // Eskiden burada Destroy(gameObject, 5) vardi. Spawn edilmis bir NetworkObject'i
        // duz Destroy ile silmek yanlis:
        //   - istemcide "NotServerException: Destroy a spawned NetworkObject on a
        //     non-host client is not valid" atar,
        //   - sunucuda nesne istemcilere duzgun despawn bildirilmeden yok olur. Gecikme
        //     varken yoldaki NetworkVariable guncellemeleri artik var olmayan bilesene
        //     dusuyor ve Netcode NetworkObject.GetNetworkBehaviourAtOrderIndex icinde
        //     NullReferenceException atiyordu (istemci logunda 5 kez goruldu).
        // Ayrica table.despawnObj ayni musteriyi 12. saniyede zaten kaldiriyordu; yani
        // birbiriyle yarisan iki silme vardi.
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            StartCoroutine(cikincaKaldir());
    }

    /// <summary>
    /// Musteri cikisa VARINCA kaldirilir (YALNIZCA sunucuda, duzgun despawn ile).
    ///
    /// Eskiden yola ciktiktan 5 saniye sonra, nerede olursa olsun kaldiriliyordu: cikis
    /// ~20 birim uzakta, musteri yolun ortasinda (cogu zaman restoranin icinde) birden
    /// yok oluyordu.
    /// </summary>
    IEnumerator cikincaKaldir()
    {
        float gecen = 0f;

        while (gecen < CIKIS_EN_UZUN)
        {
            yield return new WaitForSeconds(0.25f);
            gecen += 0.25f;

            if (entityNavMash == null || !entityNavMash.enabled || !entityNavMash.isOnNavMesh)
            {
                // Ajan NavMesh'e oturamadiysa yuruyemez; eski sureyle kaldirilir.
                if (gecen >= 5f)
                    break;
                continue;
            }

            if (gecen >= 1f && !entityNavMash.pathPending && entityNavMash.remainingDistance <= 0.6f)
                break;
        }

        if (NetworkObject != null && NetworkObject.IsSpawned)
            NetworkObject.Despawn(true); // true: nesne her tarafta yok edilir
    }

    /// <summary>
    /// Musteriyi sandalyedeki yerine kisa bir gecisle tasir.
    ///
    /// Konum sandalyenin cocugu olarak YEREL uzayda hedeflenir; araya giren kareler
    /// boyunca dunya konumu enterpole edilir. Sure kisa (0.25 sn) - oyun akisini
    /// yavaslatmadan sicramayi ortadan kaldirir.
    /// </summary>
    IEnumerator oturmaGecisi(Vector3 hedefDunya, Quaternion hedefDonus)
    {
        Vector3 basKonum = transform.position;
        Quaternion basDonus = transform.rotation;

        const float SURE = 0.25f;
        float t = 0f;

        while (t < SURE)
        {
            t += Time.deltaTime;
            float o = Mathf.SmoothStep(0f, 1f, t / SURE);

            transform.position = Vector3.Lerp(basKonum, hedefDunya, o);
            transform.rotation = Quaternion.Slerp(basDonus, hedefDonus, o);

            yield return null;
        }

        transform.position = hedefDunya;
        transform.rotation = hedefDonus;
    }

    public void setV3(Vector3 pos)
    {
        backDoorV3 = pos;
    }

    public recipe selectFood()
    {
        int count = Random.Range(1, 3+1);
        //canvasObj.SetActive(true);
        //canvasObj.transform.LookAt(Camera.main.transform);

        switch (count)
        {
            case 1:
                return recipe.COOKED_MEAT_FOOD;
            case 2:
                return recipe.BREAD_COOKED_MEATBALL_FOOD;
            case 3:
                return recipe.BREAD_COOKED_MEATBALL_LETTUCE_FOOD;
        }
        return recipe.NONE;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsHost) return;

        if (other.gameObject == targetChair && isReady == false)
            otur();
    }

    /// <summary>Musteriyi hedef sandalyesine oturtur (sunucuda).</summary>
    void otur()
    {
        {
            if (entityNavMash.enabled && entityNavMash.isOnNavMesh)
                entityNavMash.ResetPath();
            entityNavMash.enabled = false;

            rb.isKinematic = true;
            
            // Sunucu tarafi. TrySetParent kullanilir: Netcode parent'i istemcilere
            // ParentSyncMessage ile yayar ve basarisizlik artik loga duser.
            // worldPositionStays = true: orijinal kod SetParent(x) idi (varsayilan true).
            if (!NetworkObject.TrySetParent(targetChair.transform, true))
                Debug.LogWarning("[entity] Musteri sandalyeye parent'lanamadi: " + targetChair.name);

            Vector3 oncekiDunya = transform.position;
            Quaternion oncekiDonus = transform.rotation;

            transform.localPosition = Vector3.forward * 0.1f;
            transform.rotation = targetChair.transform.rotation;

            // OTURMA ARTIK KAYARAK OLUYOR.
            //
            // Olcum: musteri sandalyeye ortalama 1.4 BIRIM uzaktayken trigger'a girip
            // aninda sandalyeye yapisiyordu - ekranda net bir sicrama. Hedef konum
            // ayni kaliyor, sadece oraya bir cirpida degil kisa bir gecisle gidiliyor.
            float otururkenSicrama = Vector3.Distance(oncekiDunya, transform.position);
            if (otururkenSicrama > 0.2f)
            {
                Vector3 hedefDunya = transform.position;
                Quaternion hedefDonus = transform.rotation;

                transform.position = oncekiDunya;
                transform.rotation = oncekiDonus;

                StartCoroutine(oturmaGecisi(hedefDunya, hedefDonus));
            }

            isReady = true;

            targetTable.GetComponent<table>().isEntityReady();

            isPos = true;
        }
    }
}
