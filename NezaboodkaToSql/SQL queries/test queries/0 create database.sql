/*********************************************

	Create `nz_test_db` database for tests

**********************************************/

USE `nz_admin_db`;

/*---------------------------------------/
			Test database
--------------------------------------*/

CALL before_alter_database_list();
INSERT INTO `db_rem_list`
VALUES ('nz_test_db');
CALL alter_database_list();

CALL cleanup_removed_databases();

CALL before_alter_database_list();
INSERT INTO `db_add_list`
VALUES ('nz_test_db');
CALL alter_database_list();
