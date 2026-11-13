import 'package:flutter/material.dart';

/// Cores da marca, as mesmas do site.
class Cores {
  static const torrado = Color(0xFF3B2418);
  static const torrado2 = Color(0xFF6B5244);
  static const folha = Color(0xFF1F5C4A);
  static const folhaClara = Color(0xFFDCEAE4);
  static const cereja = Color(0xFFB3203A);
  static const kraft = Color(0xFFE7D6BB);
  static const kraftClaro = Color(0xFFF3EAD9);
  static const fundo = Color(0xFFF8F6F1);
  static const linha = Color(0xFFDDD6CA);
}

ThemeData criarTema() {
  final esquema = ColorScheme.fromSeed(
    seedColor: Cores.folha,
    primary: Cores.folha,
    secondary: Cores.cereja,
    surface: Colors.white,
  );
  // Importante: botões NUNCA usam largura mínima infinita (Size.fromHeight), porque dentro de
  // uma Row ou de um AlertDialog isso quebra o layout e a tela fica em branco.
  // Onde o botão deve ocupar a largura toda, a tela usa SizedBox(width: double.infinity).
  const tamanhoBotao = Size(64, 48);
  final formaBotao = RoundedRectangleBorder(borderRadius: BorderRadius.circular(10));

  return ThemeData(
    useMaterial3: true,
    colorScheme: esquema,
    scaffoldBackgroundColor: Cores.fundo,
    appBarTheme: const AppBarTheme(
      backgroundColor: Cores.torrado,
      foregroundColor: Cores.kraftClaro,
      centerTitle: false,
      elevation: 0,
      scrolledUnderElevation: 0,
      titleTextStyle: TextStyle(
          color: Cores.kraftClaro, fontSize: 20, fontWeight: FontWeight.w700, fontFamily: 'serif'),
    ),
    textTheme: const TextTheme(
      headlineSmall: TextStyle(fontWeight: FontWeight.w700, color: Cores.torrado, fontFamily: 'serif'),
      titleLarge: TextStyle(fontWeight: FontWeight.w700, color: Cores.torrado, fontFamily: 'serif'),
      titleMedium: TextStyle(fontWeight: FontWeight.w600, color: Cores.torrado),
      bodyMedium: TextStyle(color: Cores.torrado),
    ),
    filledButtonTheme: FilledButtonThemeData(
      style: FilledButton.styleFrom(
        minimumSize: tamanhoBotao,
        shape: formaBotao,
        textStyle: const TextStyle(fontWeight: FontWeight.w600, fontSize: 16),
      ),
    ),
    outlinedButtonTheme: OutlinedButtonThemeData(
      style: OutlinedButton.styleFrom(
        minimumSize: tamanhoBotao,
        foregroundColor: Cores.folha,
        side: const BorderSide(color: Cores.folha, width: 1.5),
        shape: formaBotao,
        textStyle: const TextStyle(fontWeight: FontWeight.w600),
      ),
    ),
    textButtonTheme: TextButtonThemeData(
      style: TextButton.styleFrom(foregroundColor: Cores.folha, shape: formaBotao),
    ),
    chipTheme: ChipThemeData(
      backgroundColor: Colors.white,
      selectedColor: Cores.folhaClara,
      checkmarkColor: Cores.folha,
      side: const BorderSide(color: Cores.linha),
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(99)),
      labelStyle: const TextStyle(color: Cores.torrado, fontWeight: FontWeight.w500),
    ),
    inputDecorationTheme: InputDecorationTheme(
      filled: true,
      fillColor: Colors.white,
      border: OutlineInputBorder(borderRadius: BorderRadius.circular(10)),
      enabledBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(10), borderSide: const BorderSide(color: Cores.linha)),
    ),
    snackBarTheme: SnackBarThemeData(
      behavior: SnackBarBehavior.floating,
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
    ),
    navigationBarTheme: const NavigationBarThemeData(
      backgroundColor: Colors.white,
      indicatorColor: Cores.folhaClara,
      elevation: 3,
      height: 68,
    ),
    // Transição suave entre telas, igual em todas as plataformas.
    pageTransitionsTheme: const PageTransitionsTheme(builders: {
      TargetPlatform.android: ZoomPageTransitionsBuilder(),
      TargetPlatform.iOS: ZoomPageTransitionsBuilder(),
      TargetPlatform.windows: ZoomPageTransitionsBuilder(),
      TargetPlatform.macOS: ZoomPageTransitionsBuilder(),
      TargetPlatform.linux: ZoomPageTransitionsBuilder(),
    }),
  );
}
