import { useEffect, useRef, useState } from 'react';
import {
  Alert,
  Animated,
  Dimensions,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  View,
} from 'react-native';
import { useQuery } from '@tanstack/react-query';
import { router } from 'expo-router';
import {
  Check,
  Crown,
  Eye,
  Filter,
  Flame,
  Heart,
  Lock,
  MessageCircle,
  Shield,
  Sparkles,
  Zap,
} from 'lucide-react-native';
import { LinearGradient } from 'expo-linear-gradient';
import * as Haptics from 'expo-haptics';
import { premiumApi } from '../../api/mindmatch';
import { Button, Card, Loading, Screen } from '../../components/ui';
import { colors } from '../../theme/tokens';

const { width: SCREEN_WIDTH } = Dimensions.get('window');

const PLANS = [
  { id: '1m', title: '1 Aylık', price: '₺99.90', perMonth: '₺99.90 / ay', badge: null },
  { id: '6m', title: '6 Aylık', price: '₺359.90', perMonth: '₺59.90 / ay', badge: 'EN POPÜLER · %40 TASARRUF', popular: true },
  { id: '12m', title: '12 Aylık VIP', price: '₺479.90', perMonth: '₺39.90 / ay', badge: 'EN İYİ DEĞER · %60 TASARRUF' },
];

const PERKS = [
  {
    icon: Filter,
    gradient: ['#8B5CF6', '#6D28D9'] as const,
    title: 'Gelişmiş Yaş & Cinsiyet Filtreleri',
    desc: 'Keşfet ekranında hedeflediğin yaş aralığına ve kriterlere göre nokta atışı eşleş.',
  },
  {
    icon: Zap,
    gradient: ['#F59E0B', '#D97706'] as const,
    title: '5 Kat Öncelikli Sıralama',
    desc: 'Açık profiller listesinde profilin daima en üst sıralarda gösterilir.',
  },
  {
    icon: Eye,
    gradient: ['#3B82F6', '#1D4ED8'] as const,
    title: 'Hayalet Modu (Ghost Mode)',
    desc: 'Kullanıcıların hikâyelerini ve profillerini iz bırakmadan, gizlice görüntüle.',
  },
  {
    icon: MessageCircle,
    gradient: ['#EC4899', '#BE185D'] as const,
    title: 'Sınırsız Mesaj İsteği',
    desc: 'Eşleşme beklemeden istediğin kişiye doğrudan ilk mesajını gönder.',
  },
  {
    icon: Lock,
    gradient: ['#10B981', '#047857'] as const,
    title: 'Buzlu (Spoiler) & Tek Seferlik Fotoğraf',
    desc: 'Sohbetlerde özel fotoğraflarını blurlu ve tek dokunuşla açılır olarak paylaş.',
  },
  {
    icon: Crown,
    gradient: ['#FBBF24', '#B45309'] as const,
    title: 'Altın VIP Rozeti & Alevli Halka',
    desc: 'Profilinde ve sohbetlerinde parıldayan Snapchat+ tarzı altın VIP taç rozeti.',
  },
];

export default function PremiumHubScreen() {
  const premium = useQuery({ queryKey: ['premium'], queryFn: premiumApi.mine });
  const [selectedPlan, setSelectedPlan] = useState('6m');

  // Pulsing animation for the 3D Crown
  const pulseAnim = useRef(new Animated.Value(1)).current;
  const glowAnim = useRef(new Animated.Value(0.4)).current;

  useEffect(() => {
    Animated.loop(
      Animated.sequence([
        Animated.parallel([
          Animated.timing(pulseAnim, { toValue: 1.08, duration: 1800, useNativeDriver: true }),
          Animated.timing(glowAnim, { toValue: 0.9, duration: 1800, useNativeDriver: true }),
        ]),
        Animated.parallel([
          Animated.timing(pulseAnim, { toValue: 1, duration: 1800, useNativeDriver: true }),
          Animated.timing(glowAnim, { toValue: 0.4, duration: 1800, useNativeDriver: true }),
        ]),
      ])
    ).start();
  }, [pulseAnim, glowAnim]);

  const isCurrentPremium = Boolean(premium.data?.isPremium);

  function handleSubscribe() {
    Haptics.notificationAsync(Haptics.NotificationFeedbackType.Success).catch(() => {});
    Alert.alert(
      '🌟 VIP Ayrıcalıkları',
      'MindMatch Premium demo modundadır. Profilindeki öncelikli görünürlük ve gelişmiş filtreler anında aktiftir!',
      [{ text: 'Harika!', style: 'default' }]
    );
  }

  return (
    <Screen scroll style={styles.screen}>
      {/* Hero Header with Animated Glowing Crown */}
      <View style={styles.heroWrap}>
        <LinearGradient
          colors={['#171329', '#0D0B16']}
          start={{ x: 0, y: 0 }}
          end={{ x: 0, y: 1 }}
          style={styles.heroCard}
        >
          {/* Animated Glow Aura */}
          <Animated.View style={[styles.glowAura, { opacity: glowAnim }]} />

          {/* Animated 3D Crown */}
          <Animated.View style={[styles.crownContainer, { transform: [{ scale: pulseAnim }] }]}>
            <LinearGradient
              colors={['#FFDF00', '#F59E0B', '#B45309']}
              start={{ x: 0, y: 0 }}
              end={{ x: 1, y: 1 }}
              style={styles.crownCircle}
            >
              <Crown size={38} color="#FFF" fill="#FFF" />
            </LinearGradient>
          </Animated.View>

          <Text style={styles.heroKicker}>MINDMATCH · VIP PLUS</Text>
          <Text style={styles.heroTitle}>Sınırları Kaldır,{'\n'}Öne Çık.</Text>
          <Text style={styles.heroDesc}>
            Snapchat+ ve Tinder Gold ayrıcalıklarıyla profilini parlat, hedeflediğin kişilerle anında buluş.
          </Text>

          {/* Current Status Pill */}
          <View style={styles.statusPill}>
            <Sparkles size={14} color="#FFDF00" />
            <Text style={styles.statusPillText}>
              {isCurrentPremium
                ? `VIP Üyeliğin Aktif · ${premium.data?.expiresAtUtc ? new Date(premium.data.expiresAtUtc).toLocaleDateString('tr-TR') : 'Süresiz'}`
                : 'Şu an Standart Üyedesin'}
            </Text>
          </View>
        </LinearGradient>
      </View>

      {/* Plan Selection Cards */}
      <View style={styles.sectionHeading}>
        <Text style={styles.sectionTitle}>Sana Uygun Planı Seç</Text>
        <Text style={styles.sectionSub}>İstediğin zaman iptal edebilirsin.</Text>
      </View>

      <View style={styles.plansContainer}>
        {PLANS.map(plan => {
          const isSelected = selectedPlan === plan.id;
          return (
            <Pressable
              key={plan.id}
              onPress={() => {
                Haptics.selectionAsync().catch(() => {});
                setSelectedPlan(plan.id);
              }}
              style={[
                styles.planCard,
                isSelected && styles.planCardSelected,
                plan.popular && styles.planCardPopular,
              ]}
            >
              {plan.badge ? (
                <View style={styles.planBadge}>
                  <Text style={styles.planBadgeText}>{plan.badge}</Text>
                </View>
              ) : null}

              <View style={styles.planCardContent}>
                <View style={{ flex: 1 }}>
                  <Text style={styles.planTitle}>{plan.title}</Text>
                  <Text style={styles.planPerMonth}>{plan.perMonth}</Text>
                </View>

                <View style={styles.planPriceWrap}>
                  <Text style={styles.planPrice}>{plan.price}</Text>
                  <View style={[styles.selectRadio, isSelected && styles.selectRadioActive]}>
                    {isSelected && <Check size={12} color="#FFF" strokeWidth={3} />}
                  </View>
                </View>
              </View>
            </Pressable>
          );
        })}
      </View>

      {/* CTA Button */}
      <Pressable onPress={handleSubscribe} style={styles.ctaButtonWrap}>
        <LinearGradient
          colors={['#FF007A', '#7928CA', '#FF5400']}
          start={{ x: 0, y: 0 }}
          end={{ x: 1, y: 0 }}
          style={styles.ctaButton}
        >
          <Crown size={18} color="#FFF" />
          <Text style={styles.ctaButtonText}>
            {isCurrentPremium ? 'VIP Ayrıcalıklarını Yönet' : 'Hemen VIP’e Yükselt 👑'}
          </Text>
        </LinearGradient>
      </Pressable>

      {/* Perks / Benefits Section */}
      <View style={[styles.sectionHeading, { marginTop: 28 }]}>
        <Text style={styles.sectionTitle}>Neden MindMatch VIP?</Text>
        <Text style={styles.sectionSub}>Snapchat & Tinder standartlarında tam kontrol</Text>
      </View>

      <View style={styles.perksList}>
        {PERKS.map((perk, i) => {
          const Icon = perk.icon;
          return (
            <Card key={i} style={styles.perkCard}>
              <LinearGradient
                colors={perk.gradient}
                start={{ x: 0, y: 0 }}
                end={{ x: 1, y: 1 }}
                style={styles.perkIconWrap}
              >
                <Icon size={20} color="#FFF" />
              </LinearGradient>

              <View style={{ flex: 1, marginLeft: 14 }}>
                <Text style={styles.perkTitle}>{perk.title}</Text>
                <Text style={styles.perkDesc}>{perk.desc}</Text>
              </View>
            </Card>
          );
        })}
      </View>

      {/* Trust & Privacy Guarantee Note */}
      <View style={styles.guaranteeBox}>
        <Shield size={16} color={colors.violet} />
        <Text style={styles.guaranteeText}>
          Tüm üyelikler şifreli altyapıyla korunur. Profil gizlilik tercihlerinden istediğin zaman görünürlüğünü açıp kapatabilirsin.
        </Text>
      </View>
    </Screen>
  );
}

const styles = StyleSheet.create({
  screen: { paddingHorizontal: 16, paddingTop: 10, paddingBottom: 40 },
  heroWrap: { marginBottom: 20 },
  heroCard: {
    borderRadius: 28,
    padding: 24,
    alignItems: 'center',
    overflow: 'hidden',
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.1)',
  },
  glowAura: {
    position: 'absolute',
    width: 220,
    height: 220,
    borderRadius: 110,
    backgroundColor: '#FFDF00',
    top: -40,
    filter: 'blur(50px)',
  },
  crownContainer: {
    marginBottom: 16,
    zIndex: 2,
  },
  crownCircle: {
    width: 76,
    height: 76,
    borderRadius: 38,
    alignItems: 'center',
    justifyContent: 'center',
    shadowColor: '#FFDF00',
    shadowOpacity: 0.7,
    shadowRadius: 18,
    elevation: 12,
  },
  heroKicker: {
    fontSize: 10,
    fontWeight: '900',
    letterSpacing: 2,
    color: '#FFDF00',
    marginBottom: 6,
  },
  heroTitle: {
    fontSize: 28,
    lineHeight: 33,
    fontWeight: '900',
    letterSpacing: -0.8,
    color: '#FFFFFF',
    textAlign: 'center',
    marginBottom: 10,
  },
  heroDesc: {
    fontSize: 13,
    lineHeight: 18,
    color: 'rgba(255, 255, 255, 0.75)',
    textAlign: 'center',
    marginBottom: 18,
    paddingHorizontal: 10,
  },
  statusPill: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    paddingHorizontal: 14,
    paddingVertical: 7,
    borderRadius: 20,
    backgroundColor: 'rgba(255, 255, 255, 0.1)',
    borderWidth: 1,
    borderColor: 'rgba(255, 223, 0, 0.3)',
  },
  statusPillText: {
    fontSize: 12,
    fontWeight: '800',
    color: '#FFDF00',
  },
  sectionHeading: { marginBottom: 14 },
  sectionTitle: { fontSize: 18, fontWeight: '900', color: colors.ink, letterSpacing: -0.4 },
  sectionSub: { fontSize: 12, color: colors.muted, marginTop: 2 },
  plansContainer: { gap: 12 },
  planCard: {
    backgroundColor: colors.white,
    borderRadius: 20,
    borderWidth: 1.5,
    borderColor: colors.line,
    padding: 16,
    overflow: 'hidden',
  },
  planCardSelected: {
    borderColor: colors.violet,
    backgroundColor: '#FAF9FF',
  },
  planCardPopular: {
    borderColor: '#F59E0B',
  },
  planBadge: {
    position: 'absolute',
    top: 0,
    right: 0,
    backgroundColor: '#F59E0B',
    paddingHorizontal: 10,
    paddingVertical: 3,
    borderBottomLeftRadius: 12,
  },
  planBadgeText: { fontSize: 9, fontWeight: '900', color: '#FFF' },
  planCardContent: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', marginTop: 4 },
  planTitle: { fontSize: 16, fontWeight: '900', color: colors.ink },
  planPerMonth: { fontSize: 12, color: colors.muted, marginTop: 3 },
  planPriceWrap: { flexDirection: 'row', alignItems: 'center', gap: 10 },
  planPrice: { fontSize: 16, fontWeight: '900', color: colors.ink },
  selectRadio: {
    width: 22,
    height: 22,
    borderRadius: 11,
    borderWidth: 2,
    borderColor: colors.line,
    alignItems: 'center',
    justifyContent: 'center',
  },
  selectRadioActive: {
    borderColor: colors.violet,
    backgroundColor: colors.violet,
  },
  ctaButtonWrap: {
    marginTop: 18,
    borderRadius: 18,
    overflow: 'hidden',
    shadowColor: '#FF007A',
    shadowOpacity: 0.35,
    shadowRadius: 10,
    elevation: 8,
  },
  ctaButton: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 8,
    paddingVertical: 16,
  },
  ctaButtonText: {
    color: '#FFF',
    fontSize: 16,
    fontWeight: '900',
    letterSpacing: 0.3,
  },
  perksList: { gap: 12 },
  perkCard: {
    flexDirection: 'row',
    alignItems: 'center',
    padding: 16,
    borderRadius: 20,
    backgroundColor: colors.white,
    borderWidth: 1,
    borderColor: colors.line,
  },
  perkIconWrap: {
    width: 44,
    height: 44,
    borderRadius: 14,
    alignItems: 'center',
    justifyContent: 'center',
  },
  perkTitle: { fontSize: 14, fontWeight: '900', color: colors.ink },
  perkDesc: { fontSize: 12, lineHeight: 17, color: colors.muted, marginTop: 3 },
  guaranteeBox: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 10,
    backgroundColor: colors.paleViolet,
    borderRadius: 16,
    padding: 14,
    marginTop: 20,
    borderWidth: 1,
    borderColor: '#EDE8FF',
  },
  guaranteeText: {
    flex: 1,
    fontSize: 11,
    lineHeight: 16,
    color: colors.muted,
  },
});
