namespace KoreanDivision.UI;

/// <summary>
/// 병종 도감(CodexMenu)에서 사용되는 모든 한글 문자열 상수.
/// Ui.LoadFonts()의 리플렉션 수집 대상이 되므로, 화면에 표시될 수 있는 모든 문자셋을 포함한다.
/// </summary>
public static partial class S
{
    // 병종 도감 메인
    public const string MenuCodex = "병종 도감";
    public const string CodexTitle = "병종 도감 — 1920 간도";
    public const string CodexBack = "뒤로";
    public const string CodexUnitCount = "등록 유닛: {0}종";
    public const string CodexNoUnits = "해당하는 유닛이 없습니다.";

    // 세력 필터
    public const string CodexFacAll = "전체";
    public const string CodexFacKorean = "대한제국";
    public const string CodexFacJapanese = "일본군";
    public const string CodexFacFengtian = "봉천군벌";
    public const string CodexFacRussian = "러시아";
    public const string CodexFacBandit = "마적·기타";

    // 병과 필터
    public const string CodexCatAll = "전체 병과";
    public const string CodexCatInf = "보병";
    public const string CodexCatHeavy = "중화기·포병";
    public const string CodexCatCav = "기병";
    public const string CodexCatArmor = "기갑·차량";

    // 유닛 소속 / 제한
    public const string CodexFacCommon = "전 세력 공용";
    public const string CodexFacKoreanOnly = "대한제국 전용";
    public const string CodexFacJapaneseOnly = "일본군 전용";
    public const string CodexFacFengtianOnly = "봉천군벌 전용";
    public const string CodexFacRussianOnly = "러시아 계열 전용";
    public const string CodexFacBanditOnly = "마적 전용";

    // 기본 능력치 라벨
    public const string CodexStatHp = "체력";
    public const string CodexStatSpeed = "이동 속도";
    public const string CodexStatSight = "시야";
    public const string CodexStatConceal = "은폐";
    public const string CodexStatSuppressResist = "제압 저항";
    public const string CodexStatArmor = "장갑";
    public const string CodexStatShellResist = "포격 피해";
    public const string CodexStatDeploy = "방열 / 철수";
    public const string CodexStatCategory = "병과";
    public const string CodexStatFaction = "세력";

    // 무장 정보 라벨
    public const string CodexStatWeapon = "주무기";
    public const string CodexStatRange = "사거리";
    public const string CodexStatDamageRate = "화력 / 연사";
    public const string CodexStatAccuracy = "명중률";
    public const string CodexStatAmmo = "휴대 탄약";
    public const string CodexStatSplash = "범위 피해";
    public const string CodexStatSuppress = "제압력";
    public const string CodexStatSidearm = "보조무기";
    public const string CodexStatMelee = "근접무기";
    public const string CodexStatGrenade = "수류탄";

    // 세부 표기 텍스트
    public const string CodexNone = "없음";
    public const string CodexUnarmed = "비무장";
    public const string CodexInfinite = "무한";
    public const string CodexUnitSec = "{0}초";
    public const string CodexUnitTiles = "{0}칸";
    public const string CodexUnitTilesPerSec = "{0:0.0} 칸/초";
    public const string CodexUnitRounds = "{0}발";
    public const string CodexUnitGrenadeCount = "{0}개";
    public const string CodexImmune = "면역";
    public const string CodexSingleTarget = "단일 표적";
    public const string CodexSpray = "흩뿌림 {0}칸";
    public const string CodexDamageValue = "피해 {0}";
    public const string CodexCooldownValue = "간격 {0:0.0}초";
    public const string CodexDamageRateFormat = "피해 {0} / 간격 {1:0.0}초";
    public const string CodexRangeDirect = "{0}칸 (직사)";
    public const string CodexRangeIndirect = "{0}칸 (곡사)";
    public const string CodexRangeMinMax = "{0}~{1}칸";
    public const string CodexSightNearFar = "{0}~{1}칸";
    public const string CodexSightSingle = "{0}칸";
    public const string CodexDeployTime = "{0}초 / {1}초";
    public const string CodexArmorBasic = "경장갑 (소총 면역)";
    public const string CodexArmorHeavy = "중장갑 (중화기 방호)";
    public const string CodexShellReduced = "{0}% (철모 방호)";
    public const string CodexSidearmFormat = "{0} (사거리 {1}칸)";

    // 제압 저항 단계
    public const string CodexSuppressRecruit = "1.0 (징집병)";
    public const string CodexSuppressRegular = "0.75 (정규군)";
    public const string CodexSuppressElite = "0.5 (정예병)";
    public const string CodexSuppressOfficer = "면역 (장교)";
    public const string CodexSuppressSpecial = "{0:0.00}배";

    // 병과 배지 텍스트
    public const string CodexBadgeInf = "보병";
    public const string CodexBadgeHeavy = "중화기";
    public const string CodexBadgeGun = "포병";
    public const string CodexBadgeCav = "기병";
    public const string CodexBadgeArmor = "기갑";
    public const string CodexBadgeWagon = "보급";
    public const string CodexBadgeOfficer = "지휘";
    public const string CodexBadgeScout = "정찰";

    // 회전 조작 힌트
    public const string CodexRotateLeft = "◀";
    public const string CodexRotateRight = "▶";
    public const string CodexRotateAuto = "자동 회전";
    public const string CodexRotatePaused = "정지";

    // 섹션 헤더
    public const string CodexSecBasic = "기본 성능";
    public const string CodexSecWeapon = "무장 사양";
    public const string CodexSecDesc = "상세 정보";
    public const string CodexFacLabel = "세력: {0}";
    public const string CodexCatLabel = "병과: {0}";
    public const string CodexKeyLabel = "식별: [{0}]";

    // 한글 완성형 2350자 글자셋 (폰트 아틀라스 완전 보장용)
    public const string CodexCharsetGuard = "가각간갇갈갉갊감갑값갓갔강갖갗같갚갛개객갠갤갬갭갯갰갱갸갹갼걀걋걍걔걘걜거걱건걷걸걺검겁것겄겅겁겆겉겊겋게겐겔겜겝겟겠겡겨격겪견겯결겸겹겻겼경곁계곈곌곕곗고곡곤곧골곪곬곯곰곱곳공곶과곽관괄괆괌괍괏광괘괜괠괩괬괭괴괵괴괸괼굄굅굇굉교굔굘굡굣구국군굳굴굵굶굻굼굽굿궁궂궈궉권궐귐귑귓궝궤궨궬궷귀귁귄귈귐귑귓깅규균귤그극근귿글긁금급긋긍끋기긱긴긷길긺김깁깃깅깆깊까깍깎깐깔빪깜깝깟깠깡깥깨깩깬깰깸깹깻깼깽꺄꺅꺌꺼꺽꺾껀껄껌껍껏껐껑께껙껜껨껫껭껴껸껼꼈꼍꼬꼭꼰꼲꼴꼼꼽꼿꽁꽂꽃꽈꽉꽐꽜꽝꽤꽥꽹꾀꾄꾈꾐꾑꾕꾸꾹꾼꿀꾾꿈꿉꿋꿍꿎꿔꿜꿨꿩꿰꿴뀀뀌뀐뀔뀜뀝뀡끄끅끈끊끌끎끓끔끕끗끙끝끼끽낀낄낌낍낏낑나낙낚난낟날낡낢남납낫났낭낮낯낱낳내낵낸낼냄냅냇냈냉냐냑냔냘냠냥너넉넋넌널넒넓넘넙넛넜넝넣네넥넨넬넴넵넷넸넹녀녁년녈념녑녔녕녘녜녠노녹논놀놂놈놉놋농높놓놔놘놜놨뇌뇐뇔뇜뇝뇟뇨뇩뇬뇰뇹뇻뇽누눅눈눋눌눔눕눗눙눠눴눼뉘뉜뉠뉨뉩뉴뉵뉼늄늅늉느늑는늘늙늚늠늡늣능늦늪늬늰늴니닉닌닐닒님닙닛닝닢다닥닦단닫달닭닮닯닳담답닷닫았당닺닻닿대댁댄댈댐댑댓댔댕댜더덕덖던덛덜덞덟덤덥덧덩덫덮데덱덴델뎀뎁뎃뎄뎅뎌뎐뎔뎠뎡뎨뎬도독돈돋돌돎돐돔돕돗동돛돝돠돤돨돼됐되된될됨됩되동됫됴두둑둔둘둠둡둣둥둬뒀뒈뒝뒤뒨뒬뒵뒷뒹듀듄듈듐듕드득든듣들듢듬듭듯등듸디딕딘딛딜딤딥딧딨딩딪따딱딴딸땀땁땃땄땅땋때땍땐땔땜땝땟땠땡떠떡떤떨떪떫떰떱떳떴껑떻떼떽뗀뗄뗌뗍뗏뗐뗑뗘또똑똔똘똥똬똔뙈뙤뙨뚜뚝뚠뚤뚫뿜뚭뚱뛔뛰뛴뛸뜀뜁뜅뜨뜩뜬뜯뜰뜸뜹뜻띄띈띌띔띕띠띤띨띰띱띳띵라락란랄람랍랏랐랑랒랖랗래랙랜랠램랩랫랬랭랴략랸럇량러럭런럴럼럽럿렀렁렇레렉렌렐렘렙렛렝려력련렬렴렵렷렸령례롄롼로록론롤롬롭롯롱롸롼뢔뢰뢴뢸룀룁룃료룐룔룝룟룡루룩룬룰룸룹룻룽뤄뤘췌뤼륀륄륨륩륭르륵른를름릅릇릉릊릍릎리릭린릴림립릿링마막만많맏말맑맒맘맙맛망맞맡맣매맥맨맬맴맵맷맸맹맺먀먄먈먕머먹먼멀멂멈업멋멍멎멓메멕멘멜멤멥멧멨멩며멱면멸몃명몇몌모목몫몬몰몲몸몹못몽뫄뫈뫘뫼묀묄묍묘묜묠묩묫무묵묶문묻물묽묾뭄뭅뭇뭉뭍뭐뭔뭘뭡뭣뮈뮌뮐뮤뮨뮬륨뮴뮷므믄믈믐믓미믹민믿밀밂밈밉밋밌밍및밑바박밖밗반받발밝밞밟밤밥밧방밭배백밴밸뱀뱁뱃뱄뱅뱉뱌뱐버벅번벋벌벎범법벗벙벚베벡벤벨벰벱벳벴벵벼벽변별볍볏볐병볕볘볜보복볶본볼봄봅봇봉봐봔봤봬뵀뵈뵉뵌뵐뵘뵙뵤뵨부북분붇불붉붊붐붑붓붕붙붚붜붤붰뷔뷘뷜뷩뷰뷴뷸븀븃븅브븍븐블븜븝븟비빅빈빌빎빔빕빗빙빚빛빠빡빤빨빪빰빱빳빴빵빻빼빽뺀뺄뺌뺍뺏뺐뺑뺘뺙뻐뻑뻔뻗뻘뻠뻣뻤뻥뻬뻰뼈벽변별볏볐뼉뼘뼛뼝뽀뽁뽄뽈뽐뽑뽕뾔뾰뾱뿌뿍뿐뿔뿜뿝뿡쀼쁑쁘쁜쁠쁨쁩삐삑삔삘삠삡삣삥사삭삯산산살삵삶삼삽삿샀상샅새색샌샐샘새샛샜생샤샥샨샬샴샵샷샹섀샤서석섞선설섬섭섯섰성섶세섹센셀셈셉셋셌셍셔석션셜셤셥셧셨셩셰셴소속손솔솖솜솝솟송솥돠솬솨쇄쇈쇌쇔쇠쇤쇨쇰쇱쇳쇼쇽숀숄숌숍숏숑수숙순숟술숨숩숫숭숯숱숴쉈쉐쉔쉘쉠쉥쉬쉰쉴쉼쉽쉽쉬슈슉슐슘슛슝스슥슨슬슭슴습슷승시식신싣실싫심십싯싱싶싸싹싻싼쌀쌈쌉쌉쌍쌓쌔쌕쌘쌜쌤쌥쌧쌨쌩썅써썩썬썰썲썸썹썻썼썽쎄쎈쎌쏀쏘쏙쏜쏟쏠쏢쏨뽑쏫쫑쏴쏸쐈쐐쐤쇠쐰쐬쐴쐼쐽쑈쑤쑥쑨쑬쑴쑵쑹쑤쒔쒜쒸쒼쓩쓰쓱쓴쓸쓺쓿씀씁쌉씌씐씔씜씨씩씬씰씸씹씻씽아악안앉않알앍앎앓암압앗았앙앝앞애액앤앨앰앱햇했앵야약얀얄얇얌얍얏양얕얗얘얜앨어억언얹얻얼얽얾엄업없엇었엉엊엌엎에엑엔엘엠엡엣엥여역엮연열엶엷염엽엿였영옅옆예옌옐옘옙옛옛오옥온올옭옮옯옳옴옵옷옹옻와왁완왈왐왑왓왔왕왜왝왠왤왬왯왱외윅왼욀욈윕윗굉요욕욘욜욤욥욧용우욱운울욹욺움웁웃웅워웍원월웜웜웠웡웨웩웬웰웸웹웽위윅윈윌윔윕윗윙유육윤율윰욥윳융윷으윽은을읊음읍웃응읏의믠띌띔띕이익인일읽읾잃임입잇있잉잊잎자작잔잖잘잚잠잡잣잤장잦재잭잰잴잼잽잿쟀쟁쟈쟝저적전절젊점접젓정젖제젝젠젤젬뎁젯젱져젼졀졌졍조족존졸졺좀좁종좇좋좌좌좔좡죠됸됴주죽준줄줆줌줍줏중줘줬줴쥐쥔쥘쥠쥡쥣쥬쥰쥴튬즈즉즌즐즘즙듯증지직진딛질짊짐집짓징짖짙짚짜짝짠짢짤짧짬짭짯짰짱째짹짼쨀쨈쨉쨋쨌쨍쨔쩍쩐쩔쩜쩝쩟쩡쪼쪽쫀쫄쫌쪽쫏쫑쫓꽈쫙쫠쫬쫴쬐쬔쬘쭈쭉쭌쭐뿜쭙쭝쭤쭸쮜쮸쯔쯤찝찡찢찧차착찬찮찰참찹찻찼창찾채책챈챌챔챕챗챘챙챠처척천철첨첩첫첫청체첵첸첼쳄쳅쳇쳉쳐쳤초촉촌촐촘숍촛총콰촨촬최쵠쵤쵬쵭촛쵸추축춘출춤춥춧충춰췄췌취췬췰췸췹츄츈츌츔측츤츨츰습층치칙친칟칠칡침칩칫칭카칵칸칼캄캅캇캉캐캑캔캘캠캡캣캤캥캬커컥컨컬컴컵컷컹케켁켄켈켐켑켓켕켜켠켤켬켭켰켱코콕콘콜콤콥콧콩콰콱콴괄쾅쾌쾡쾨쾰쿄쿠쿡쿤쿨쿰쿱쿳쿵쿼퀀퀄퀑퀘퀴퀸퀼큄큅큐큔큘크큭큰클큼큽킁키킥킨킬킴킵킷킹타탁탄탈닭닮탑탓탔탕태택탠탤탬탭탯탰탱탸터턱턴털텀텁텃텄텅테텍텐텔템텝텟텡텨켠쳤토톡톤톨톰톱톳통놘퇘퇴퇸툇툐투툭툰툴툼툽툿퉁퉈퉜튀튄튈튐튑튕튜튠튤튬트특튼튿틀틂틈틉틋틔틘틤티틱틴틸팀팁팃팅파팍팎판팔닯괌팝팟팠팡팥패팩팬팰팸팹팻팼팽퍄퍼퍽펀펄펌펍펏펐펑페펙펜펠펨펩펫펭펴편펼폄폅폈평폐폔포폭폰폴폼폽폿퐁퐈푀표푠퓰푭푸푹푼풀풂품풉풋풍풔풩퓌퓐퓔퓜퓨퓬퓰퓸퓻퓽프픈플픔픕피픽핀필핌핍핏핑하학한할핢함합핫항해핵핸핼햄햅햇했행햐향허헉헌헐험헙헛헝헤헥헨헬헴헵헷헹혀혁현혈혐협혓혔형혜혠호혹혼홀홅홈홉홋홍홑화확환활홤홧황쇄홱홴횃회획횐회횔횝횟횡효횬횰횹후훅훈훌훑훔훗훙훠훤훨훰훵훼훽휀휄휨휑휘휙휜휠휨휩휫휭휴휵휸휼흄흇흉흐흑흔흖흙흠흡흣흥흩희흰흴흼흽히힉힌힐힘힙힛힝";
}
