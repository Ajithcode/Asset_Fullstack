import 'package:flutter/material.dart';

import 'package:asset360_mobile/core/widgets/widgets.dart';
import 'package:asset360_mobile/features/more/account/backend_connection_card.dart';

/// Backend API: base URLs and Test connection, outside Administration so it stays
/// open whatever the licence (Settings → Developer → Backend API).
class BackendApiScreen extends StatelessWidget {
  const BackendApiScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return AppScaffold(
      body: ListView(
        padding: context.pagePadding,
        children: const [
          PageTitle(
            title: 'Backend API',
            subtitle: 'Base URLs and Test connection',
            help: 'The servers this build talks to. Test connection checks each service.',
          ),
          SizedBox(height: 12),
          BackendConnectionCard(),
        ],
      ),
    );
  }
}
