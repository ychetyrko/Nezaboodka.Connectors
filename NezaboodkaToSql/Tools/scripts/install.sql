/* ********************************************

	Create Nezaboodka administrative database
			and nezaboodka users

********************************************* */

CREATE DATABASE `nz_admin_db` DEFAULT CHARSET=`UTF8` COLLATE `UTF8_BIN`;
USE `nz_admin_db`;


/* ********************************************

		Administrative database tables
*/

/*	Databases list

	User rights:
		0 - ReadWrite
		1 - ReadOnly
		2 - NoAccess
*/
CREATE TABLE `db_list`(
	`name` VARCHAR(64) PRIMARY KEY NOT NULL UNIQUE
		CHECK(`name` != ''),
	`access` TINYINT UNSIGNED NOT NULL DEFAULT 0	-- ReadWrite
		CHECK(`access` < 3),
	`is_removed` BOOLEAN NOT NULL DEFAULT FALSE
) ENGINE=`INNODB` COLLATE `UTF8_GENERAL_CI`;



/* ********************************************

			Stored procedures
*/

DELIMITER //
DROP PROCEDURE IF EXISTS QEXEC //
CREATE PROCEDURE QEXEC(
	IN query_text TEXT
)
BEGIN
	DECLARE is_prepared BOOLEAN DEFAULT FALSE;
	DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN
		SET @prep_str = NULL;
		IF is_prepared THEN
			DEALLOCATE PREPARE p_prep_proc;
		END IF;
		RESIGNAL;
	END;

	SET @qexec_row_count = 0;
	SET @prep_str = query_text;

	PREPARE p_prep_proc FROM @prep_str;
	SET is_prepared = TRUE;

	EXECUTE p_prep_proc;
	SET @qexec_row_count = ROW_COUNT();

	DEALLOCATE PREPARE p_prep_proc;
	SET @prep_str = NULL;
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _prepare_new_database //
CREATE PROCEDURE _prepare_new_database(
	IN db_name VARCHAR(64)
)
BEGIN
/*---------------------------------------/
			Schema Info
--------------------------------------*/
	CALL QEXEC(CONCAT(	-- type
	"
CREATE TABLE `", db_name, "`.`type`(
	`id` INT PRIMARY KEY NOT NULL AUTO_INCREMENT,
	`name` VARCHAR(128) NOT NULL UNIQUE
		CHECK(`name` != ''),
	`table_name` VARCHAR(64) NOT NULL UNIQUE COLLATE `UTF8_GENERAL_CI`
		CHECK(`table_name` != ''),
	`base_type_name` VARCHAR(128) NOT NULL DEFAULT ''
) ENGINE=`INNODB`;

	"));
	CALL QEXEC(CONCAT(	-- type_closure
	"
CREATE TABLE `", db_name, "`.`type_closure`(
	`ancestor` INT NOT NULL,
	`descendant` INT NOT NULL,

	FOREIGN KEY(`ancestor`)
		REFERENCES `type`(`id`)
		ON DELETE CASCADE,

	FOREIGN KEY(`descendant`)
		REFERENCES `type`(`id`)
		ON DELETE CASCADE,

	CONSTRAINT `uc_keys`
		UNIQUE (`ancestor`, `descendant`)
) ENGINE=`INNODB`;

	"));
	CALL QEXEC(CONCAT(	-- field
	"
CREATE TABLE `", db_name, "`.`field` (
	`id` INT PRIMARY KEY AUTO_INCREMENT NOT NULL UNIQUE,
	`owner_type_name` VARCHAR(128) NOT NULL
		CHECK(`owner_type_name` != ''),
	`owner_type_id` INT DEFAULT NULL,
	`name` VARCHAR(128) NOT NULL
		CHECK(`name` != ''),
	`col_name` VARCHAR(64) NOT NULL COLLATE `UTF8_GENERAL_CI`
		CHECK(`col_name` != ''),
	`type_name` VARCHAR(64) NOT NULL
		CHECK(`type_name` != ''),
	`is_nullable` BOOLEAN NOT NULL DEFAULT FALSE,
	`ref_type_id` INT DEFAULT NULL,
	`is_list` BOOLEAN NOT NULL DEFAULT FALSE,
	`compare_options` ENUM (
		'None',
		'IgnoreCase',
		'IgnoreNonSpace',
		'IgnoreSymbols',
		'IgnoreKanaType',
		'IgnoreWidth',
		'OrdinalIgnoreCase',
		'StringSort',
		'Ordinal'
	) NOT NULL DEFAULT 'None',
	`back_ref_name` VARCHAR(128) DEFAULT NULL
		CHECK(`back_ref_name` != ''),
	`back_ref_id` INT DEFAULT NULL,

	FOREIGN KEY(`owner_type_id`)
		REFERENCES `type`(`id`)
		ON DELETE CASCADE,

	FOREIGN KEY(`ref_type_id`)
		REFERENCES `type`(`id`)
		ON DELETE RESTRICT,	-- to prevent deletion of referenced type

	FOREIGN KEY(`back_ref_id`)
		REFERENCES `field`(`id`)
		ON DELETE SET NULL,

	CONSTRAINT `uc_type_fields`
		UNIQUE (`owner_type_name`, `name`),

	CONSTRAINT `uc_table_columns`
		UNIQUE (`owner_type_name`, `col_name`)
) ENGINE=`INNODB`;

	"));
	CALL QEXEC(CONCAT(	-- index base
	"
CREATE TABLE `", db_name, "`.`index_base` (
	`id` INT PRIMARY KEY AUTO_INCREMENT NOT NULL,
	`target_type_id` INT NOT NULL,
	`index_type` ENUM (
		'secondary',
		'referencial'
		) NOT NULL,

	-- referencial index
	`type_id` INT NOT NULL,
	`field_id` INT,

	-- secondary index
	`is_unique` BOOLEAN NOT NULL,

	FOREIGN KEY (`target_type_id`)
		REFERENCES `type`(`id`)
		ON DELETE CASCADE,

	FOREIGN KEY (`type_id`)
		REFERENCES `type`(`id`)
		ON DELETE CASCADE,

	FOREIGN KEY (`field_id`)
		REFERENCES `field`(`id`)
		ON DELETE CASCADE
) ENGINE=`INNODB`;

	"));
	CALL QEXEC(CONCAT(	-- index field
	"
CREATE TABLE `", db_name, "`.`index_field` (
	`id` INT PRIMARY KEY AUTO_INCREMENT NOT NULL,
	`index_id` INT NOT NULL,
	`field_id` INT NOT NULL,
	`ordering` ENUM (
		'ASC',
		'DESC'
	) NOT NULL,

	FOREIGN KEY (`index_id`)
		REFERENCES `index_base`(`id`)
		ON DELETE CASCADE,

	FOREIGN KEY (`field_id`)
		REFERENCES `field`(`id`)
		ON DELETE CASCADE
) ENGINE=`INNODB`;

	"));

/*---------------------------------------/
			Default Structure
--------------------------------------*/
	CALL QEXEC(CONCAT(	-- db_key
	"
CREATE TABLE `", db_name, "`.`db_key` (
	`sys_id` BIGINT(0) PRIMARY KEY AUTO_INCREMENT NOT NULL UNIQUE,
	`real_type_id` INT NOT NULL,

	FOREIGN KEY (`real_type_id`)
		REFERENCES `type`(`id`)
		ON DELETE CASCADE	-- delete key when it's type info is removed
) ENGINE=`INNODB`;

	"));
	CALL QEXEC(CONCAT(	-- list
	"
CREATE TABLE `", db_name, "`.`list` (
	`id` INT PRIMARY KEY AUTO_INCREMENT NOT NULL UNIQUE,
	`owner_id` BIGINT(0) NOT NULL,
	`field_id` INT NOT NULL,

	FOREIGN KEY (`owner_id`)
		REFERENCES `db_key`(`sys_id`)
		ON DELETE CASCADE,	-- cleanup list of deleted object

	FOREIGN KEY (`field_id`)
		REFERENCES `field`(`id`)
		ON DELETE CASCADE	-- delete list when it's field info is removed
) ENGINE=`INNODB`;

	"));
	CALL QEXEC(CONCAT(	-- list_item
	"
CREATE TABLE `", db_name, "`.`list_item` (
	`id` INT PRIMARY KEY AUTO_INCREMENT NOT NULL UNIQUE,
	`list_id` INT NOT NULL,
	`ref` BIGINT(0) NOT NULL,

	FOREIGN KEY (`list_id`)
		REFERENCES `list`(`id`)
		ON DELETE CASCADE,	-- clear removed lists

	FOREIGN KEY (`ref`)
		REFERENCES `db_key`(`sys_id`)
		ON DELETE CASCADE,	-- remove deleted object from all lists

	CONSTRAINT `uc_list_ref`	-- no duplicates
		UNIQUE (`list_id`, `ref`)
) ENGINE=`INNODB`;

	"));
END //


/*********************************************
		Alter database list
*/
/*	Protocol for altering database list:

	1. call `before_alter_database_list` to create temporary tables if not created;
	2. fill `db_rem_list` and `db_add_list` tables with database names;
	3. call `alter_database_list`.
*/

DELIMITER //
DROP PROCEDURE IF EXISTS before_alter_database_list //
CREATE PROCEDURE before_alter_database_list()
BEGIN
-- List of databases to create
	DROP TEMPORARY TABLE IF EXISTS `nz_admin_db`.`db_add_list`;
	CREATE TEMPORARY TABLE `nz_admin_db`.`db_add_list`(
		`name` VARCHAR(64) NOT NULL UNIQUE
	) ENGINE=`MEMORY` COLLATE `UTF8_GENERAL_CI`;

-- List of databases to drop
	DROP TEMPORARY TABLE IF EXISTS `nz_admin_db`.`db_rem_list`;
	CREATE TEMPORARY TABLE IF NOT EXISTS `nz_admin_db`.`db_rem_list`(
		`name` VARCHAR(64) NOT NULL UNIQUE
	) ENGINE=`MEMORY` COLLATE `UTF8_GENERAL_CI`;
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _remove_databases //
CREATE PROCEDURE _remove_databases()
BEGIN
	UPDATE `db_list`
	SET `is_removed` = TRUE
	WHERE `name` IN (
		SELECT `name`
		FROM `db_rem_list`
	);
END //


DELIMITER //
DROP PROCEDURE IF EXISTS cleanup_removed_databases //
CREATE PROCEDURE cleanup_removed_databases()
BEGIN
	DECLARE done INT DEFAULT FALSE;
	DECLARE db_name VARCHAR(64);
	DECLARE cur CURSOR FOR
		SELECT `name` FROM `db_list`
		WHERE `is_removed`;
	DECLARE CONTINUE HANDLER FOR NOT FOUND
		SET done = TRUE;

	OPEN cur;
	FETCH cur INTO db_name;
	WHILE NOT done DO
		CALL QEXEC(CONCAT(
			"DROP DATABASE IF EXISTS ", db_name
		));
		FETCH cur INTO db_name;
	END WHILE;
	CLOSE cur;

	DELETE FROM `db_list`
	WHERE `is_removed`;
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _add_databases //
CREATE PROCEDURE _add_databases()
BEGIN
	INSERT INTO `db_list`
	(`name`)
	SELECT `name`
	FROM `db_add_list`; 
END //


DELIMITER //
DROP PROCEDURE IF EXISTS alter_database_list //
CREATE PROCEDURE alter_database_list()
BEGIN
	DECLARE EXIT HANDLER FOR SQLEXCEPTION
	BEGIN
		ROLLBACK;
		CALL _cleanup_temp_alter_database_list();
		RESIGNAL;
	END;

	START TRANSACTION;

	CALL _remove_databases();
	CALL _add_databases();
	
	COMMIT;

-- Create new databases
	BEGIN
		DECLARE done INT DEFAULT FALSE;
		DECLARE db_name VARCHAR(64);
		DECLARE cur CURSOR FOR
			SELECT `name`
			FROM `db_add_list`;
		DECLARE CONTINUE HANDLER FOR NOT FOUND
			SET done = TRUE;

		OPEN cur;
		FETCH cur INTO db_name;
		WHILE NOT done DO
			CALL QEXEC(CONCAT(
				"CREATE DATABASE `", db_name, "`;"
			));
			CALL _prepare_new_database(db_name);
			FETCH cur INTO db_name;
		END WHILE;
		CLOSE cur;
	END;

	CALL _cleanup_temp_alter_database_list();
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _cleanup_temp_alter_database_list //
CREATE PROCEDURE _cleanup_temp_alter_database_list()
BEGIN
	DROP TEMPORARY TABLE IF EXISTS `db_rem_list`;
	DROP TEMPORARY TABLE IF EXISTS `db_add_list`;
END //


/*********************************************
		Alter database schema
*/
/*	Protocol for altering database schema:

	1. call `before_alter_database_schema`;
	2. fill `type_add_list`, `type_rem_list`, `field_add_list` and `field_rem_list` tables;
	3. call `alter_database_schema`.
*/

/*---------------------------------------/
			Public routines
--------------------------------------*/

DELIMITER //
DROP PROCEDURE IF EXISTS before_alter_database_schema //
CREATE PROCEDURE before_alter_database_schema()
BEGIN
	CALL _before_alter_fields();
	CALL _before_alter_types();
	CALL _before_alter_back_refs();
END //


DELIMITER //
DROP PROCEDURE IF EXISTS alter_database_schema //
CREATE PROCEDURE alter_database_schema(
	IN db_name varchar(64)
)
BEGIN
	DECLARE EXIT HANDLER FOR SQLEXCEPTION
	BEGIN
		ROLLBACK;
		CALL _cleanup_temp_tables_after_alter_db_schema();
		SET @db_name = NULL;
		RESIGNAL;
	END;

	-- To move all queries that provide implicit commit to the end of execution
	DROP TEMPORARY TABLE IF EXISTS `nz_admin_db`.`alter_query`;
	CREATE TEMPORARY TABLE `nz_admin_db`.`alter_query`(
		`ord` INT NOT NULL UNIQUE AUTO_INCREMENT,
		`query_text` TEXT DEFAULT NULL
	) ENGINE=`INNODB`;

	SET @db_name = db_name;

	CALL _temp_before_common();
	CALL _temp_before_remove_fields();
	CALL _temp_before_remove_types();
	CALL _temp_before_add_types();
	CALL _temp_before_add_fields();

	START TRANSACTION;

	CALL _remove_back_refs();
	CALL _remove_fields();
	CALL _remove_types();
	CALL _add_types();
	CALL _add_fields();
	CALL _add_back_refs();

	COMMIT;

-- Apply all changes
	BEGIN
		DECLARE q_text TEXT DEFAULT NULL;
		
		DECLARE done BOOLEAN DEFAULT FALSE;
		DECLARE query_cur CURSOR FOR
			SELECT `query_text`
			FROM `nz_admin_db`.`alter_query`;
		DECLARE CONTINUE HANDLER FOR NOT FOUND
			SET done = TRUE;

		OPEN query_cur;

		FETCH query_cur
		INTO q_text;
		WHILE NOT done DO
/*
-- Debug
			SELECT q_text;
*/
			CALL QEXEC(q_text);

			FETCH query_cur
			INTO q_text;
		END WHILE;

		CLOSE query_cur;
	END;

	CALL _cleanup_temp_tables_after_alter_db_schema();
	SET @db_name = NULL;
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _cleanup_temp_tables_after_alter_db_schema //
CREATE PROCEDURE _cleanup_temp_tables_after_alter_db_schema()
BEGIN
	CALL _temp_after_common();
	CALL _temp_after_remove_fields();
	CALL _temp_after_remove_types();
	CALL _temp_after_add_types();
	CALL _temp_after_add_fields();
	DROP TEMPORARY TABLE IF EXISTS `nz_admin_db`.`alter_query`;
END //


/*---------------------------------------/
			Common routines
--------------------------------------*/

DELIMITER //
DROP PROCEDURE IF EXISTS _temp_before_common //
CREATE PROCEDURE _temp_before_common()
BEGIN
	DROP TEMPORARY TABLE IF EXISTS `nz_admin_db`.`temp_type_fields`;
	CREATE TEMPORARY TABLE `nz_admin_db`.`temp_type_fields`(
		`id` INT PRIMARY KEY AUTO_INCREMENT NOT NULL UNIQUE,
		`col_name` VARCHAR(64) NOT NULL COLLATE `UTF8_GENERAL_CI`
			CHECK(`col_name` != ''),
		`type_name` VARCHAR(64) NOT NULL
			CHECK(`type_name` != ''),
		`is_nullable` BOOLEAN NOT NULL DEFAULT FALSE,
		`ref_type_id` INT DEFAULT NULL,
		`is_list` BOOLEAN NOT NULL DEFAULT FALSE,
		`compare_options` ENUM (
			'None',
			'IgnoreCase',
			'IgnoreNonSpace',
			'IgnoreSymbols',
			'IgnoreKanaType',
			'IgnoreWidth',
			'OrdinalIgnoreCase',
			'StringSort',
			'Ordinal'
		) NOT NULL DEFAULT 'None'
	) ENGINE=`MEMORY`;

-- Shadow tables

	DROP TEMPORARY TABLE IF EXISTS `nz_admin_db`.`type_shadow`;
	CREATE TEMPORARY TABLE `nz_admin_db`.`type_shadow`(
		`id` INT NOT NULL,
		`name` VARCHAR(128) NOT NULL UNIQUE,
		`table_name` VARCHAR(64) NOT NULL UNIQUE COLLATE `UTF8_GENERAL_CI`,
		`base_type_name` VARCHAR(128) NOT NULL DEFAULT ''
	) ENGINE=`MEMORY`;

	DROP TEMPORARY TABLE IF EXISTS `nz_admin_db`.`type_shadow_base`;
	CREATE TEMPORARY TABLE `nz_admin_db`.`type_shadow_base`
	LIKE `nz_admin_db`.`type_shadow`;

	DROP TEMPORARY TABLE IF EXISTS `nz_admin_db`.`type_closure_shadow`;
	CREATE TEMPORARY TABLE `nz_admin_db`.`type_closure_shadow`(
		`ancestor` INT NOT NULL,
		`descendant` INT NOT NULL
	) ENGINE=`MEMORY`;

	DROP TEMPORARY TABLE IF EXISTS `nz_admin_db`.`field_shadow`;
	CREATE TEMPORARY TABLE `nz_admin_db`.`field_shadow` (
		`id` INT NOT NULL,
		`owner_type_name` VARCHAR(128) NOT NULL,
		`owner_type_id` INT DEFAULT NULL,
		`name` VARCHAR(128) NOT NULL,
		`col_name` VARCHAR(64) NOT NULL COLLATE `UTF8_GENERAL_CI`,
		`type_name` VARCHAR(64) NOT NULL,
		`is_nullable` BOOLEAN NOT NULL DEFAULT FALSE,
		`ref_type_id` INT DEFAULT NULL,
		`is_list` BOOLEAN NOT NULL DEFAULT FALSE,
		`compare_options` ENUM (
			'None',
			'IgnoreCase',
			'IgnoreNonSpace',
			'IgnoreSymbols',
			'IgnoreKanaType',
			'IgnoreWidth',
			'OrdinalIgnoreCase',
			'StringSort',
			'Ordinal'
		) NOT NULL DEFAULT 'None',
		`back_ref_name` VARCHAR(128) DEFAULT NULL,
		`back_ref_id` INT DEFAULT NULL
	) ENGINE=`MEMORY`;
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _init_type_shadow //
CREATE PROCEDURE _init_type_shadow(
	source_db_name VARCHAR(64)
)
BEGIN
	DELETE FROM `nz_admin_db`.`type_shadow`;
	CALL QEXEC(CONCAT(
		"INSERT INTO `nz_admin_db`.`type_shadow`
		(`id`, `name`, `table_name`, `base_type_name`)
		SELECT `id`, `name`, `table_name`, `base_type_name`
		FROM `", source_db_name, "`.`type`;"
	));
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _init_type_shadow_base //
CREATE PROCEDURE _init_type_shadow_base(
	source_db_name VARCHAR(64)
)
BEGIN
	DELETE FROM `nz_admin_db`.`type_shadow_base`;
	CALL QEXEC(CONCAT(
		"INSERT INTO `nz_admin_db`.`type_shadow_base`
		(`id`, `name`, `table_name`, `base_type_name`)
		SELECT `id`, `name`, `table_name`, `base_type_name`
		FROM `", source_db_name, "`.`type`;"
	));
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _init_type_closure_shadow //
CREATE PROCEDURE _init_type_closure_shadow(
	source_db_name VARCHAR(64)
)
BEGIN
	DELETE FROM `nz_admin_db`.`type_closure_shadow`;
	CALL QEXEC(CONCAT(
		"INSERT INTO `nz_admin_db`.`type_closure_shadow`
		(`ancestor`, `descendant`)
		SELECT `ancestor`, `descendant`
		FROM `", source_db_name, "`.`type_closure`;"
	));
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _init_field_shadow //
CREATE PROCEDURE _init_field_shadow(
	source_db_name VARCHAR(64)
)
BEGIN
	DELETE FROM `nz_admin_db`.`field_shadow`;
	CALL QEXEC(CONCAT(
		"INSERT INTO `nz_admin_db`.`field_shadow`
		(`id`, `owner_type_name`, `owner_type_id`, `name`, `col_name`, `type_name`, `is_nullable`, `ref_type_id`, `is_list`, `compare_options`, `back_ref_name`, `back_ref_id`)
		SELECT `id`, `owner_type_name`, `owner_type_id`, `name`, `col_name`, `type_name`, `is_nullable`, `ref_type_id`, `is_list`, `compare_options`, `back_ref_name`, `back_ref_id`
		FROM `", source_db_name, "`.`field`;"
	));
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _temp_after_common //
CREATE PROCEDURE _temp_after_common()
BEGIN
	DROP TEMPORARY TABLE IF EXISTS `nz_admin_db`.`field_shadow`;
	DROP TEMPORARY TABLE IF EXISTS `nz_admin_db`.`type_closure_shadow`;
	DROP TEMPORARY TABLE IF EXISTS `nz_admin_db`.`type_shadow`;

	DROP TEMPORARY TABLE IF EXISTS `nz_admin_db`.`temp_type_fields`;   
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _get_type_new_fields_and_constraints //
CREATE PROCEDURE _get_type_new_fields_and_constraints(
	IN c_type_id INT,
	IN inheriting BOOLEAN,
	OUT fields_defs TEXT,
	OUT fields_constraints TEXT
)
BEGIN
	DECLARE cf_id INT DEFAULT NULL;	-- for constraints names
	DECLARE cf_col_name VARCHAR(64) DEFAULT NULL;
	DECLARE cf_type_name VARCHAR(128) DEFAULT NULL;
	DECLARE cf_ref_type_id INT DEFAULT NULL;
	DECLARE cf_is_nullable BOOLEAN DEFAULT FALSE;
	DECLARE cf_is_list BOOLEAN DEFAULT FALSE;
	DECLARE cf_compare_options VARCHAR(64);

	SET fields_defs = '';
	SET fields_constraints = '';

	DELETE FROM `nz_admin_db`.`temp_type_fields`;
	IF inheriting THEN	-- get all parents' fields
		CALL QEXEC(CONCAT(
			"INSERT INTO `nz_admin_db`.`temp_type_fields`
			(`id`, `col_name`, `type_name`, `is_nullable`, `ref_type_id`,
				`is_list`, `compare_options`)
			SELECT f.`id`, f.`col_name`, f.`type_name`, f.`is_nullable`, f.`ref_type_id`,
				f.`is_list`, f.`compare_options`
			FROM `", @db_name, "`.`field` AS f
			WHERE f.`owner_type_id` IN (
				SELECT clos.`ancestor`	-- get all super classes
				FROM `", @db_name, "`.`type_closure` AS clos
				WHERE clos.`descendant` = ", c_type_id, "
			);"
		));
	ELSE	-- get only NEW fields
		CALL QEXEC(CONCAT(
			"INSERT INTO `nz_admin_db`.`temp_type_fields`
			(`id`, `col_name`, `type_name`, `is_nullable`, `ref_type_id`,
				`is_list`, `compare_options`)
			SELECT f.`id`, f.`col_name`, f.`type_name`, f.`is_nullable`, f.`ref_type_id`,
				f.`is_list`, f.`compare_options`
			FROM `nz_admin_db`.`new_field` AS newf	-- only new fields
			LEFT JOIN `", @db_name, "`.`field` AS f
			ON f.`id` = newf.`id`
			WHERE f.`owner_type_id` IN (
				SELECT clos.`ancestor`	-- get all super classes
				FROM `", @db_name, "`.`type_closure` AS clos
				WHERE clos.`descendant` = ", c_type_id, "
			);"
		));
	END IF;

	BEGIN	
		DECLARE fields_done BOOLEAN DEFAULT FALSE;
		DECLARE fields_cur CURSOR FOR
			SELECT `id`, `col_name`, `type_name`, `is_nullable`, `ref_type_id`,
				`is_list`, `compare_options`
			FROM `nz_admin_db`.`temp_type_fields`;
		DECLARE CONTINUE HANDLER FOR NOT FOUND
			SET fields_done = TRUE;

		OPEN fields_cur;

		FETCH fields_cur
		INTO cf_id, cf_col_name, cf_type_name, cf_is_nullable, cf_ref_type_id,
			cf_is_list, cf_compare_options;
		WHILE NOT fields_done DO
			CALL _update_type_fields_def_constr(
				fields_defs, fields_constraints, inheriting,
				c_type_id, cf_id, cf_col_name, cf_type_name, cf_is_nullable, cf_ref_type_id,
				cf_is_list, cf_compare_options
			);
			FETCH fields_cur
			INTO cf_id, cf_col_name, cf_type_name, cf_is_nullable, cf_ref_type_id,
				cf_is_list, cf_compare_options;
		END WHILE;
	END;

	IF (LEFT(fields_defs, 1) = ',') THEN
		SET fields_defs = SUBSTRING(fields_defs, 2);
	END IF;

	IF (LEFT(fields_constraints, 1) = ',') THEN
		SET fields_constraints = SUBSTRING(fields_constraints, 2);
	END IF;
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _update_type_fields_def_constr //
CREATE PROCEDURE _update_type_fields_def_constr(
	INOUT f_defs TEXT,
	INOUT f_constrs TEXT,
	IN inheriting BOOLEAN,
	IN c_type_id INT,
	IN cf_id INT,
	IN cf_col_name VARCHAR(64),
	IN cf_type_name VARCHAR(128),
	IN cf_is_nullable BOOLEAN,
	IN cf_ref_type_id INT,
	IN cf_is_list BOOLEAN,
	IN cf_compare_options VARCHAR(128)
)
BEGIN
	DECLARE constr_add_prefix TEXT DEFAULT 'CONSTRAINT FK_';
	DECLARE constr_add_prefix_full TEXT DEFAULT '';
	DECLARE field_type VARCHAR(128);

	IF NOT inheriting THEN
		SET constr_add_prefix = CONCAT('ADD ', constr_add_prefix);
	END IF;

	IF cf_ref_type_id IS NULL THEN
		IF NOT cf_is_list THEN
			SET field_type = cf_type_name;
			IF field_type LIKE 'VARCHAR(%' OR field_type = 'TEXT' THEN
				IF cf_compare_options = 'IgnoreCase' THEN
					SET field_type = CONCAT(field_type, ' COLLATE `utf8_general_ci`');
				END IF;
			ELSE	-- not string
				IF (NOT cf_is_nullable) THEN
					SET field_type = CONCAT(field_type, ' NOT NULL');
				END IF;
			END IF;
			
		ELSE	-- list
			SET field_type = 'BLOB';
		END IF;
	ELSE	-- reference
		-- FK Constraint name = FK_<type_id>_<field_id>
		SET constr_add_prefix_full = CONCAT('
			', constr_add_prefix, c_type_id, '_', cf_id);
		
		IF NOT cf_is_list THEN
			SET field_type = 'BIGINT(0)';
			SET f_constrs = CONCAT(f_constrs, ',
				', constr_add_prefix_full,'
				FOREIGN KEY (`', cf_col_name,'`)
					REFERENCES `db_key`(`sys_id`)
					ON DELETE SET NULL
					ON UPDATE SET NULL');
		ELSE	-- list
			SET field_type = 'INT';
			SET f_constrs = CONCAT(f_constrs, ',
				', constr_add_prefix_full,'
				FOREIGN KEY (`', cf_col_name,'`)
					REFERENCES `list`(`id`)
					ON DELETE SET NULL');
		END IF;
	END IF;

	SET f_defs = CONCAT(f_defs, ', `', cf_col_name, '` ', field_type);
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _remove_deleted_fields_from_table //
CREATE PROCEDURE _remove_deleted_fields_from_table()
BEGIN
	CALL QEXEC(CONCAT(
		"UPDATE `", @db_name, "`.`field`
		SET `back_ref_name` = NULL
		WHERE `back_ref_id` IN (
			SELECT `id`
			FROM `nz_admin_db`.`removing_fields_list`
		);"
	));
	CALL QEXEC(CONCAT(
		"DELETE FROM `", @db_name, "`.`field`
		WHERE `id` IN (
			SELECT `id`
			FROM `nz_admin_db`.`removing_fields_list`
		);"
	));
END //


/*---------------------------------------/
			Fields routines
--------------------------------------*/

DELIMITER //
DROP PROCEDURE IF EXISTS _before_alter_fields //
CREATE PROCEDURE _before_alter_fields()
BEGIN
	DROP TEMPORARY TABLE IF EXISTS `nz_admin_db`.`field_add_list`;
	CREATE TEMPORARY TABLE IF NOT EXISTS `nz_admin_db`.`field_add_list`(
		`owner_type_name` VARCHAR(128) NOT NULL
			CHECK(`owner_type_name` != ''),
		`name` VARCHAR(128) NOT NULL
			CHECK(`name` != ''),
		`col_name` VARCHAR(64) NOT NULL COLLATE `UTF8_GENERAL_CI`
			CHECK(`col_name` != ''),
		`type_name` VARCHAR(64) NOT NULL
			CHECK(`type_name` != ''),
		`is_nullable` BOOLEAN NOT NULL DEFAULT FALSE,
		`is_list` BOOLEAN NOT NULL DEFAULT FALSE,
		`compare_options` ENUM (
			'None',
			'IgnoreCase',
			'IgnoreNonSpace',
			'IgnoreSymbols',
			'IgnoreKanaType',
			'IgnoreWidth',
			'OrdinalIgnoreCase',
			'StringSort',
			'Ordinal'
		) NOT NULL DEFAULT 'None',

		CONSTRAINT `uc_type_fields`
			UNIQUE (`owner_type_name`, `name`),

		CONSTRAINT `uc_table_columns`
			UNIQUE (`owner_type_name`, `col_name`)
	) ENGINE=`MEMORY`;

	DROP TEMPORARY TABLE IF EXISTS `nz_admin_db`.`field_rem_list`;
	CREATE TEMPORARY TABLE IF NOT EXISTS `nz_admin_db`.`field_rem_list`(
		`owner_type_name` VARCHAR(128) NOT NULL
			CHECK(`owner_type_name` != ''),
		`name` VARCHAR(128) NOT NULL
			CHECK(`name` != ''),

		CONSTRAINT `uc_pair`
			UNIQUE (`owner_type_name`, `name`)
	);
END //


-- Add fields

DELIMITER //
DROP PROCEDURE IF EXISTS _temp_before_add_fields //
CREATE PROCEDURE _temp_before_add_fields()
BEGIN
	DROP TEMPORARY TABLE IF EXISTS `nz_admin_db`.`new_field`;
	CREATE TEMPORARY TABLE IF NOT EXISTS `nz_admin_db`.`new_field`(
		`id` INT NOT NULL
	) ENGINE=`MEMORY`;

	DROP TEMPORARY TABLE IF EXISTS `nz_admin_db`.`fields_check_list`;
	CREATE TEMPORARY TABLE `nz_admin_db`.`fields_check_list`(
		`name` VARCHAR(128) NOT NULL UNIQUE
	) ENGINE=`MEMORY`;
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _temp_after_add_fields //
CREATE PROCEDURE _temp_after_add_fields()
BEGIN
	DROP TEMPORARY TABLE IF EXISTS `nz_admin_db`.`fields_check_list`;
	DROP TEMPORARY TABLE IF EXISTS `nz_admin_db`.`new_field`;
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _add_fields //
CREATE PROCEDURE _add_fields()
BEGIN
	DECLARE EXIT HANDLER FOR SQLEXCEPTION
	BEGIN
		SIGNAL SQLSTATE 'HY000'
			SET MESSAGE_TEXT = "Some fields can't be added";
	END;

	CALL QEXEC(CONCAT(
		"INSERT INTO `", @db_name, "`.`field`
		(`name`, `col_name`, `owner_type_name`, `type_name`, `is_nullable`, `is_list`, `compare_options`, `owner_type_id`, `ref_type_id`)
		SELECT newf.`name`, newf.`col_name`, newf.`owner_type_name`, newf.`type_name`, newf.`is_nullable`, newf.`is_list`, newf.`compare_options`, ownt.`id`, reft.`id`
		FROM `nz_admin_db`.`field_add_list` AS newf
		JOIN `", @db_name, "`.`type` AS ownt
		ON ownt.`name` = newf.`owner_type_name`
		LEFT JOIN `", @db_name, "`.`type` AS reft
		ON reft.`name` = newf.`type_name`;"
	));
	CALL QEXEC(CONCAT(
		"INSERT INTO `nz_admin_db`.`new_field`
		SELECT f.`id`
		FROM `", @db_name, "`.`field` AS f
		JOIN `nz_admin_db`.`field_add_list` AS newf
		ON f.`name` = newf.`name`
			AND f.`owner_type_name` = newf.`owner_type_name`;"
	));

	CALL _check_types_duplicate_fields();
	CALL _update_types_add_fields();

	DELETE FROM `nz_admin_db`.`field_add_list`;
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _check_types_duplicate_fields //
CREATE PROCEDURE _check_types_duplicate_fields()
BEGIN
	DECLARE t_type_id VARCHAR(128) DEFAULT NULL;

	DECLARE types_done BOOLEAN DEFAULT FALSE;
	DECLARE type_cur CURSOR FOR
		SELECT t.`id`
		FROM `nz_admin_db`.`type_shadow` AS t;
	DECLARE CONTINUE HANDLER FOR NOT FOUND
		SET types_done = TRUE;

	CALL _init_type_shadow(@db_name);

	OPEN type_cur;

	FETCH type_cur	
	INTO t_type_id;
	WHILE NOT types_done DO
		CALL _check_type_fields(t_type_id);
		FETCH type_cur	
		INTO t_type_id;
	END WHILE;

	DELETE FROM `nz_admin_db`.`fields_check_list`;
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _check_type_fields //
CREATE PROCEDURE _check_type_fields(
	IN t_type_id INT UNSIGNED
)
BEGIN
	DELETE FROM `nz_admin_db`.`fields_check_list`;
	CALL QEXEC(CONCAT(
		"INSERT INTO `nz_admin_db`.`fields_check_list`
		(`name`)
		SELECT `name`
		FROM `", @db_name, "`.`field`
		WHERE `owner_type_id` IN (
			SELECT `ancestor`	-- get all super classes
			FROM `", @db_name, "`.`type_closure`
			WHERE `descendant` = ", t_type_id, "
		);"
    ));
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _update_types_add_fields //
CREATE PROCEDURE _update_types_add_fields()
BEGIN
	DECLARE t_type_id VARCHAR(128) DEFAULT NULL;
	DECLARE t_table_name VARCHAR(64) DEFAULT NULL;
	DECLARE fields_defs TEXT;
	DECLARE fields_constraints TEXT;

	DECLARE types_done BOOLEAN DEFAULT FALSE;
	DECLARE type_cur CURSOR FOR
		SELECT t.`id` ,t.`table_name`
		FROM `nz_admin_db`.`type_shadow` AS t;
	DECLARE CONTINUE HANDLER FOR NOT FOUND
		SET types_done = TRUE;
	
	OPEN type_cur;

	FETCH type_cur	
	INTO t_type_id, t_table_name;
	WHILE NOT types_done DO
		CALL _get_type_new_fields_and_constraints(
			t_type_id, FALSE, fields_defs, fields_constraints
		);

		IF (LENGTH(fields_defs) > 0) THEN
			IF (LENGTH(fields_constraints) > 0) THEN 
				SET fields_constraints = CONCAT(',', fields_constraints);
			END IF;

			SET @prep_str = CONCAT('
				ALTER TABLE `', @db_name, '`.`', t_table_name, '`
					ADD COLUMN (', fields_defs ,')
					', fields_constraints, ';
				');

			-- check if types are valid
			PREPARE p_alter_table FROM @prep_str;
			DEALLOCATE PREPARE p_alter_table;

			INSERT INTO `nz_admin_db`.`alter_query`
			(`query_text`)
			VALUE
			(@prep_str);
		END IF;

		FETCH type_cur
		INTO t_type_id, t_table_name;
	END WHILE;

	CLOSE type_cur;
END //


-- Remove fields

DELIMITER //
DROP PROCEDURE IF EXISTS _temp_before_remove_fields //
CREATE PROCEDURE _temp_before_remove_fields()
BEGIN
	DROP TEMPORARY TABLE IF EXISTS `nz_admin_db`.`removing_fields_list`;
	CREATE TEMPORARY TABLE `nz_admin_db`.`removing_fields_list`(
		`id` INT NOT NULL UNIQUE
	) ENGINE=`MEMORY`;
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _temp_after_remove_fields //
CREATE PROCEDURE _temp_after_remove_fields()
BEGIN
	DROP TEMPORARY TABLE IF EXISTS `nz_admin_db`.`removing_fields_list`;
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _remove_fields //
CREATE PROCEDURE _remove_fields()
BEGIN
	DECLARE rem_fields_count INT DEFAULT 0;
	DECLARE real_rem_fields_count INT DEFAULT 0;

	DECLARE EXIT HANDLER FOR SQLEXCEPTION
	BEGIN
		RESIGNAL;
		SIGNAL SQLSTATE 'HY000'
			SET MESSAGE_TEXT = "Some fields can't be removed";
	END;

	SELECT COUNT(`name`)
	INTO rem_fields_count
	FROM `nz_admin_db`.`field_rem_list`;

	CALL QEXEC(CONCAT(
		"INSERT INTO `nz_admin_db`.`removing_fields_list`
		SELECT f.`id`
		FROM `", @db_name, "`.`field` AS f
		JOIN `nz_admin_db`.`field_rem_list` AS remf
		ON f.`owner_type_name` = remf.`owner_type_name`
			AND f.`name` = remf.`name`;"
	));

	SELECT COUNT(`id`)
	INTO real_rem_fields_count
	FROM `nz_admin_db`.`removing_fields_list`;

	IF (real_rem_fields_count != rem_fields_count) THEN
		SIGNAL SQLSTATE 'HY000';
	END IF;

	CALL _update_types_remove_fields();

	CALL _remove_deleted_fields_from_table();
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _update_types_remove_fields //
CREATE PROCEDURE _update_types_remove_fields()
BEGIN
	DECLARE c_type_id INT DEFAULT NULL;
	DECLARE t_table_name VARCHAR(64) DEFAULT NULL;
	DECLARE drop_columns TEXT DEFAULT '';
	DECLARE drop_constraints TEXT DEFAULT '';

	DECLARE types_done BOOLEAN DEFAULT FALSE;
	DECLARE type_cur CURSOR FOR
		SELECT t.`id` ,t.`table_name`
		FROM `nz_admin_db`.`type_shadow` AS t;
	DECLARE CONTINUE HANDLER FOR NOT FOUND
		SET types_done = TRUE;

	CALL _init_type_shadow(@db_name);
	CALL _init_type_closure_shadow(@db_name);
	CALL _init_field_shadow(@db_name);

	OPEN type_cur;

	FETCH type_cur	
	INTO c_type_id, t_table_name;
	WHILE NOT types_done DO
/*
-- Debug
		SELECT c_type_id AS 'Current type id';
*/
		CALL _get_type_removed_fields_and_constraints(
			c_type_id, drop_constraints, drop_columns
		);

		IF (LENGTH(drop_columns) > 0) THEN
			IF (LENGTH(drop_constraints) > 0) THEN 
				SET drop_constraints = CONCAT(drop_constraints, ',');
			END IF;
			SET @prep_str = CONCAT('
				ALTER TABLE `', @db_name, '`.`', t_table_name, '`
					', drop_constraints,'
					', drop_columns,'
				');
/*
-- Debug
			SELECT @prep_str AS 'Altering query';
*/
			INSERT INTO `nz_admin_db`.`alter_query`
			(`query_text`)
			VALUE
			(@prep_str);
		END IF;

		FETCH type_cur
		INTO c_type_id, t_table_name;
	END WHILE;

	CLOSE type_cur;
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _get_type_removed_fields_and_constraints //
CREATE PROCEDURE _get_type_removed_fields_and_constraints(
	IN cf_owner_type_id INT,
	OUT drop_constraints TEXT,
	OUT drop_columns TEXT
)
BEGIN
	DECLARE cf_col_name VARCHAR(64) DEFAULT NULL;
	DECLARE cf_ref_type_id INT DEFAULT NULL;
	DECLARE cf_id INT DEFAULT NULL;

	DECLARE done BOOLEAN DEFAULT FALSE;
	DECLARE fields_cur CURSOR FOR
		SELECT f.`col_name`, f.`id`, f.`ref_type_id`
		FROM `nz_admin_db`.`removing_fields_list` AS remf
		JOIN `nz_admin_db`.`field_shadow` AS f
		ON remf.`id` = f.`id`
		WHERE f.`owner_type_id` IN (
			SELECT clos.`ancestor`	-- get all super classes
			FROM `nz_admin_db`.`type_closure_shadow` AS clos
			WHERE clos.`descendant` = cf_owner_type_id
		);
	DECLARE CONTINUE HANDLER FOR NOT FOUND
		SET done = TRUE;

	SET drop_constraints = '';
	SET drop_columns = '';

	OPEN fields_cur;

	FETCH fields_cur
	INTO cf_col_name, cf_id, cf_ref_type_id;
	WHILE NOT done DO
		SET drop_columns = CONCAT(drop_columns, ',
			DROP COLUMN ', cf_col_name
		);
		
		IF !(cf_ref_type_id IS NULL) THEN
			SET drop_constraints = CONCAT(drop_constraints, ',
			DROP FOREIGN KEY FK_', cf_owner_type_id, '_', cf_id);
		END IF;
		
		FETCH fields_cur
		INTO cf_col_name, cf_id, cf_ref_type_id;
	END WHILE;
	
	IF (LEFT(drop_columns, 1) = ',') THEN
		SET drop_columns = SUBSTRING(drop_columns, 2);
	END IF;
	
	IF (LEFT(drop_constraints, 1) = ',') THEN
		SET drop_constraints = SUBSTRING(drop_constraints, 2);
	END IF;
END //


/*---------------------------------------/
			Types routines
--------------------------------------*/

DELIMITER //
DROP PROCEDURE IF EXISTS _before_alter_types //
CREATE PROCEDURE _before_alter_types()
BEGIN
	DROP TEMPORARY TABLE IF EXISTS `nz_admin_db`.`type_add_list`;
	CREATE TEMPORARY TABLE IF NOT EXISTS `nz_admin_db`.`type_add_list`(
		`name` VARCHAR(128) NOT NULL UNIQUE
			CHECK(`name` != ''),
		`table_name` VARCHAR(64) NOT NULL UNIQUE COLLATE `UTF8_GENERAL_CI`
			CHECK(`table_name` != ''),
		`base_type_name` VARCHAR(128) NOT NULL DEFAULT ''
	) ENGINE=`MEMORY`;

	DROP TEMPORARY TABLE IF EXISTS `nz_admin_db`.`type_rem_list`;
	CREATE TEMPORARY TABLE IF NOT EXISTS `nz_admin_db`.`type_rem_list`(
		`name` VARCHAR(128) NOT NULL UNIQUE
			CHECK(`name` != '')
	) ENGINE=`MEMORY`;
END //


-- Add types

DELIMITER //
DROP PROCEDURE IF EXISTS _temp_before_add_types //
CREATE PROCEDURE _temp_before_add_types()
BEGIN
	DROP TEMPORARY TABLE IF EXISTS `nz_admin_db`.`type_add_queue`;
	CREATE TEMPORARY TABLE `nz_admin_db`.`type_add_queue`(
		`ord` INT NOT NULL,
		`id` INT NOT NULL
	) ENGINE=`MEMORY`;

	DROP TEMPORARY TABLE IF EXISTS `nz_admin_db`.`new_type`;
	CREATE TEMPORARY TABLE `nz_admin_db`.`new_type`(
		`id` INT NOT NULL
	) ENGINE=`MEMORY`;
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _temp_after_add_types //
CREATE PROCEDURE _temp_after_add_types()
BEGIN
	DROP TEMPORARY TABLE IF EXISTS `nz_admin_db`.`new_type`;
	DROP TEMPORARY TABLE IF EXISTS `nz_admin_db`.`type_add_queue`;
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _add_types //
CREATE PROCEDURE _add_types()
BEGIN
	DECLARE inserted_types_count INT DEFAULT 0;
	DECLARE add_types_count INT DEFAULT 0;

	DECLARE EXIT HANDLER FOR SQLEXCEPTION
	BEGIN
		SIGNAL SQLSTATE 'HY000'
			SET MESSAGE_TEXT = "Some types can't be added";
	END;

	CALL _order_insert_new_types();

	SELECT COUNT(`id`)
	INTO inserted_types_count
	FROM `nz_admin_db`.`type_add_queue`;
    
    SELECT COUNT(`name`)
	INTO add_types_count
	FROM `nz_admin_db`.`type_add_list`;

	IF (inserted_types_count != add_types_count) THEN
		SIGNAL SQLSTATE '40011';
	END IF;

	DELETE FROM `nz_admin_db`.`type_add_list`;

	CALL _add_new_types_to_closure();
	CALL _process_new_types();
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _order_insert_new_types //
CREATE PROCEDURE _order_insert_new_types()
BEGIN
	DECLARE last_insert_count INT DEFAULT 0;
	DECLARE current_ord INT DEFAULT 0;

	SET current_ord = 0;
	CALL _ord_insert_roots(current_ord);

	ORDER_LOOP: LOOP
		SET current_ord = current_ord + 1;

		CALL _ord_insert_existing_children(current_ord, last_insert_count);

		IF last_insert_count = 0 THEN
			LEAVE ORDER_LOOP;
		END IF;
	END LOOP;
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _ord_insert_roots //
CREATE PROCEDURE _ord_insert_roots(
	IN current_ord INT
)
BEGIN
	DECLARE type_id INT;

	DECLARE t_name VARCHAR(128);
	DECLARE t_base_name VARCHAR(128);
	DECLARE t_table_name VARCHAR(64);

	DECLARE done BOOLEAN DEFAULT FALSE;
	DECLARE cur CURSOR FOR
		SELECT `name`, `table_name`
		FROM `nz_admin_db`.`type_add_list`
		WHERE `base_type_name` = '';
	DECLARE CONTINUE HANDLER FOR NOT FOUND
		SET done = TRUE;

	OPEN cur;
	FETCH cur
	INTO t_name, t_table_name;

	WHILE NOT done DO
		CALL QEXEC(CONCAT(
			"INSERT INTO `", @db_name, "`.`type`
			(`name`, `base_type_name`, `table_name`)
			VALUE
			('", t_name, "', '', '", t_table_name, "');"
		));

		SELECT LAST_INSERT_ID()
		INTO type_id;

		INSERT INTO `nz_admin_db`.`type_add_queue`
		(`ord`, `id`)
		VALUE
		(current_ord, type_id);

		INSERT INTO `nz_admin_db`.`new_type`
		(`id`)
		VALUE 
		(type_id);

        FETCH cur
		INTO t_name, t_table_name;
	END WHILE;
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _ord_insert_existing_children //
CREATE PROCEDURE _ord_insert_existing_children(
	IN current_ord INT,
	OUT insert_count INT
)
BEGIN
	DECLARE type_id INT;
	DECLARE last_insert_count INT DEFAULT 0;

	DECLARE t_name VARCHAR(128);
	DECLARE t_base_name VARCHAR(128);
	DECLARE t_table_name VARCHAR(64);

	DECLARE done BOOLEAN DEFAULT FALSE;
	DECLARE cur CURSOR FOR
		SELECT tadd.`name`, tadd.`base_type_name`, tadd.`table_name`
		FROM `nz_admin_db`.`type_add_list` AS tadd
		JOIN `nz_admin_db`.`type_shadow` AS t
		ON tadd.`base_type_name` = t.`name`;
	DECLARE CONTINUE HANDLER FOR NOT FOUND
		SET done = TRUE;

	CALL _init_type_shadow(@db_name);
	SET insert_count = 0;

	OPEN cur;
	FETCH cur
	INTO t_name, t_base_name, t_table_name;
	WHILE NOT done DO
		SET @t_base_name = t_base_name;
		SET @@SESSION.last_insert_id = 0;
		CALL QEXEC(CONCAT(
			"INSERT IGNORE INTO `", @db_name, "`.`type`
			(`name`, `base_type_name`, `table_name`)
			VALUE
			('", t_name, "', @t_base_name, '", t_table_name, "');"
		));

		SELECT LAST_INSERT_ID()
		INTO type_id;
		IF (type_id != 0) THEN
			SET insert_count = insert_count + 1;

			INSERT INTO `nz_admin_db`.`type_add_queue`
			(`ord`, `id`)
			VALUE
			(current_ord, type_id);

			INSERT INTO `nz_admin_db`.`new_type`
			(`id`)
			VALUE 
			(type_id);
		END IF;

		FETCH cur
		INTO t_name, t_base_name, t_table_name;
	END WHILE;

	SET @t_base_name = NULL;
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _add_new_types_to_closure //
CREATE PROCEDURE _add_new_types_to_closure()
BEGIN
	DECLARE base_id INT DEFAULT NULL;
	DECLARE type_id INT DEFAULT NULL;
	
	DECLARE done BOOLEAN DEFAULT FALSE;
	DECLARE cur CURSOR FOR
		SELECT tadd.`id`, tbase.`id`
		FROM `nz_admin_db`.`type_shadow` AS tadd
		JOIN `nz_admin_db`.`type_add_queue` AS q
		ON tadd.`id` = q.`id`
		LEFT JOIN `nz_admin_db`.`type_shadow_base` AS tbase
		ON tbase.`name` = tadd.`base_type_name`
		ORDER BY q.`ord`;
	DECLARE CONTINUE HANDLER FOR NOT FOUND
		SET done = TRUE;

	CALL _init_type_shadow(@db_name);
	CALL _init_type_shadow_base(@db_name);

	OPEN cur;
	FETCH cur
	INTO type_id, base_id;
	WHILE NOT done DO
		SET @base_id = base_id;
		CALL QEXEC(CONCAT(
			"INSERT INTO `", @db_name, "`.`type_closure`
			(`ancestor`, `descendant`)
			SELECT clos.`ancestor`, ", type_id, "
			FROM `", @db_name, "`.`type_closure` AS clos
			WHERE clos.`descendant` = @base_id
			UNION
			SELECT ", type_id, ", ", type_id, ";"
		));
		FETCH cur
		INTO type_id, base_id;
	END WHILE;
	CLOSE cur;
	SET @base_id = NULL;
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _process_new_types //
CREATE PROCEDURE _process_new_types()
BEGIN
	DECLARE t_type_id INT DEFAULT NULL;
	DECLARE t_table_name VARCHAR(64) DEFAULT NULL;
	DECLARE fields_defs TEXT;
	DECLARE fields_constraints TEXT;

	DECLARE types_done BOOLEAN DEFAULT FALSE;
	DECLARE new_type_cur CURSOR FOR
		SELECT t.`id` ,t.`table_name`
		FROM `nz_admin_db`.`type_shadow` AS t
		JOIN `nz_admin_db`.`new_type` AS n
		WHERE t.`id` = n.`id`;
	DECLARE CONTINUE HANDLER FOR NOT FOUND
		SET types_done = TRUE;

	CALL _init_type_shadow(@db_name);

	OPEN new_type_cur;

	FETCH new_type_cur	
	INTO t_type_id, t_table_name;
	WHILE NOT types_done DO

		CALL _get_type_new_fields_and_constraints(
			t_type_id, TRUE, fields_defs, fields_constraints
		);

		IF (CHAR_LENGTH(fields_defs) > 0) THEN 
			SET fields_defs = CONCAT(',', fields_defs);
		END IF;

		IF (CHAR_LENGTH(fields_constraints) > 0) THEN 
			SET fields_constraints = CONCAT(',', fields_constraints);
		END IF;

		SET @prep_str = CONCAT('
			CREATE TABLE `', @db_name, '`.`', t_table_name, '` (
				id BIGINT(0) PRIMARY KEY NOT NULL

				', fields_defs ,',

				FOREIGN KEY (id)
					REFERENCES `db_key`(`sys_id`)
					ON DELETE CASCADE	-- delete object when it\'s key is removed

					', fields_constraints, '

			) ENGINE=`InnoDB`;
		');
/*
-- Debug
		SELECT @prep_str;
*/
		-- check if types are valid
		PREPARE p_create_table FROM @prep_str;
		DEALLOCATE PREPARE p_create_table;

		INSERT INTO `nz_admin_db`.`alter_query`
		(`query_text`)
		VALUE
		(@prep_str);

		FETCH new_type_cur
		INTO t_type_id, t_table_name;
	END WHILE;
	
	CLOSE new_type_cur;
END //


-- Remove types

DELIMITER //
DROP PROCEDURE IF EXISTS _temp_before_remove_types //
CREATE PROCEDURE _temp_before_remove_types()
BEGIN
	DROP TEMPORARY TABLE IF EXISTS `nz_admin_db`.`removing_types_list`;
	CREATE TEMPORARY TABLE `nz_admin_db`.`removing_types_list`(
		`id` INT NOT NULL UNIQUE,
		`constr` TEXT NOT NULL,
		`table_name` VARCHAR(64) NOT NULL
	) ENGINE=`INNODB`;	-- TEXT is not supported in MEMORY engine

	DROP TEMPORARY TABLE IF EXISTS `nz_admin_db`.`removing_fields_list`;
	CREATE TEMPORARY TABLE `nz_admin_db`.`removing_fields_list`(
		`id` INT NOT NULL UNIQUE
	) ENGINE=`MEMORY`;

	DROP TEMPORARY TABLE IF EXISTS `nz_admin_db`.`removing_types_buf`;
	CREATE TEMPORARY TABLE `nz_admin_db`.`removing_types_buf`(
		`id` INT NOT NULL UNIQUE,
		`name` VARCHAR(128) NOT NULL UNIQUE CHECK(`name` != '')
	) ENGINE=`MEMORY`;
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _temp_after_remove_types //
CREATE PROCEDURE _temp_after_remove_types()
BEGIN
	DROP TEMPORARY TABLE IF EXISTS `nz_admin_db`.`removing_types_buf`;
	DROP TEMPORARY TABLE IF EXISTS `nz_admin_db`.`removing_fields_list`;
	DROP TEMPORARY TABLE IF EXISTS `nz_admin_db`.`removing_types_list`;
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _remove_types //
CREATE PROCEDURE _remove_types()
BEGIN
	DECLARE EXIT HANDLER FOR SQLEXCEPTION
	BEGIN
		SIGNAL SQLSTATE 'HY000'
			SET MESSAGE_TEXT = "Some types can't be removed";
	END;

	CALL _get_removing_types_constr();

	CALL _remove_types_fields_from_table();
	CALL _remove_types_from_closure();

	CALL QEXEC(CONCAT(
		"DELETE FROM `", @db_name, "`.`type`
		WHERE `id` IN (
			SELECT `id`
			FROM `nz_admin_db`.`removing_types_list`
		);"
	));
	CALL _process_removed_types();
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _get_removing_types_constr //
CREATE PROCEDURE _get_removing_types_constr()
BEGIN
	DECLARE t_type_id INT DEFAULT NULL;
	DECLARE t_table_name VARCHAR(64) DEFAULT NULL;
	DECLARE dropping_constraints TEXT DEFAULT '';

	DECLARE types_done BOOLEAN DEFAULT FALSE;
	DECLARE type_cur CURSOR FOR
		SELECT t.`id` ,t.`table_name`
		FROM `nz_admin_db`.`type_shadow` AS t
		JOIN `nz_admin_db`.`type_rem_list` AS remt
		WHERE t.`name` = remt.`name`;
	DECLARE CONTINUE HANDLER FOR NOT FOUND
		SET types_done = TRUE;

	CALL _init_type_shadow(@db_name);
	CALL _init_type_closure_shadow(@db_name);
	CALL _init_field_shadow(@db_name);

	OPEN type_cur;

	FETCH type_cur	
	INTO t_type_id, t_table_name;
	WHILE NOT types_done DO
		CALL _get_type_constraints(t_type_id, dropping_constraints);
		
		IF (CHAR_LENGTH(dropping_constraints) > 0) THEN 
			SET dropping_constraints = CONCAT(
				'ALTER TABLE `', @db_name, '`.`', t_table_name, '`
				', dropping_constraints, ';'
			);
		END IF;

		INSERT INTO `nz_admin_db`.`removing_types_list`
		(`id`, `table_name`, `constr`)
		VALUE
		(t_type_id, t_table_name, dropping_constraints);

		FETCH type_cur	
		INTO t_type_id, t_table_name;
	END WHILE;
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _get_type_constraints //
CREATE PROCEDURE _get_type_constraints(
	IN c_type_id INT,
	OUT drop_constraints TEXT
)
BEGIN
	DECLARE cf_col_name VARCHAR(64) DEFAULT NULL;
	DECLARE cf_ref_type_id INT DEFAULT NULL;
	DECLARE cf_id INT DEFAULT NULL;

	DECLARE done BOOLEAN DEFAULT FALSE;
	DECLARE fields_cur CURSOR FOR
		SELECT `id`, `ref_type_id`
		FROM `nz_admin_db`.`field_shadow` 
		WHERE `owner_type_id` IN (
			SELECT clos.`ancestor`	-- get all super classes
			FROM `nz_admin_db`.`type_closure_shadow` AS clos
			WHERE clos.`descendant` = c_type_id
		);
	DECLARE CONTINUE HANDLER FOR NOT FOUND
		SET done = TRUE;

	SET drop_constraints = '';

	OPEN fields_cur;

	FETCH fields_cur
	INTO cf_id, cf_ref_type_id;
	WHILE NOT done DO
		IF !(cf_ref_type_id IS NULL) THEN
			SET drop_constraints = CONCAT(drop_constraints, ',
			DROP FOREIGN KEY `FK_', c_type_id, '_', cf_id, '`');
		END IF;
		
		FETCH fields_cur
		INTO cf_id, cf_ref_type_id;
	END WHILE;
	
	IF (LEFT(drop_constraints, 1) = ',') THEN
		SET drop_constraints = SUBSTRING(drop_constraints, 2);
	END IF;
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _remove_types_fields_from_table //
CREATE PROCEDURE _remove_types_fields_from_table()
BEGIN
	CALL QEXEC(CONCAT(
		"INSERT INTO `nz_admin_db`.`removing_fields_list`
		(`id`)
		SELECT `id`
		FROM `", @db_name, "`.`field`
		WHERE `owner_type_id` IN (
			SELECT `id`
			FROM `nz_admin_db`.`removing_types_list`
		);"
	));
	CALL _remove_deleted_fields_from_table();
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _remove_types_from_closure //
CREATE PROCEDURE _remove_types_from_closure()
BEGIN
	DECLARE rest_count INT DEFAULT 0;

	LP_TERM: LOOP
		CALL QEXEC(CONCAT(
			"INSERT INTO `nz_admin_db`.`removing_types_buf`
			(`id`, `name`)
			SELECT t.`id`, t.`name`
			FROM `", @db_name, "`.`type` AS t
			JOIN `nz_admin_db`.`removing_types_list` AS remtlist
			ON t.`id` = remtlist.`id`
			WHERE (
				SELECT COUNT(clos.`ancestor`)
				FROM `", @db_name, "`.`type_closure` AS clos
				WHERE clos.`ancestor` = t.`id`
			) = 1;"	-- Сheck if terminating type
		));

		IF (@qexec_row_count = 0) THEN
			LEAVE LP_TERM;
		END IF;

		CALL QEXEC(CONCAT(
			"DELETE FROM `", @db_name, "`.`type_closure`
			WHERE `descendant` IN (
				SELECT `id`
				FROM `nz_admin_db`.`removing_types_buf`
			);"
		));

		DELETE FROM `nz_admin_db`.`type_rem_list`
		WHERE `name` IN (
			SELECT `name`
			FROM `nz_admin_db`.`removing_types_buf`
		);

		DELETE FROM `nz_admin_db`.`removing_types_buf`;
	END LOOP;

	SELECT COUNT(`name`)
	INTO rest_count
	FROM `nz_admin_db`.`type_rem_list`;

	IF (rest_count > 0) THEN
		SIGNAL SQLSTATE 'HY000';
	END IF;
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _process_removed_types //
CREATE PROCEDURE _process_removed_types()
BEGIN
	DECLARE t_type_id INT DEFAULT NULL;
	DECLARE t_table_name VARCHAR(64) DEFAULT NULL;
	DECLARE dropping_constraints TEXT DEFAULT '';

	DECLARE types_done BOOLEAN DEFAULT FALSE;
	DECLARE rem_type_cur CURSOR FOR
		SELECT `id`, `table_name`, `constr`
		FROM `nz_admin_db`.`removing_types_list`;
	DECLARE CONTINUE HANDLER FOR NOT FOUND
		SET types_done = TRUE;

	OPEN rem_type_cur;

	FETCH rem_type_cur	
	INTO t_type_id, t_table_name, dropping_constraints;
	WHILE NOT types_done DO
		IF (dropping_constraints != '') THEN
			SET @prep_str = dropping_constraints;

			INSERT INTO `nz_admin_db`.`alter_query`
			(`query_text`)
			VALUE
			(@prep_str);
		END IF;

		SET @prep_str = CONCAT('DROP TABLE `', @db_name, '`.`', t_table_name, '`;');

		INSERT INTO `nz_admin_db`.`alter_query`
		(`query_text`)
		VALUE
		(@prep_str);

		FETCH rem_type_cur
		INTO t_type_id, t_table_name, dropping_constraints;
	END WHILE;
	
	CLOSE rem_type_cur;
END //


/*---------------------------------------/
		Back References routines
--------------------------------------*/

DELIMITER //
DROP PROCEDURE IF EXISTS _before_alter_back_refs //
CREATE PROCEDURE _before_alter_back_refs()
BEGIN
	DROP TEMPORARY TABLE IF EXISTS `nz_admin_db`.`backref_upd_list`;
	CREATE TEMPORARY TABLE IF NOT EXISTS `nz_admin_db`.`backref_upd_list`(
		`field_owner_type_name` VARCHAR(128) NOT NULL
			CHECK(`owner_type_name` != ''),
		`field_name` VARCHAR(128) NOT NULL
			CHECK(`name` != ''),
		`new_back_ref_name` VARCHAR(128) DEFAULT NULL
			CHECK(`back_ref_name` != ''),

		CONSTRAINT `uc_pair`
			UNIQUE (`field_owner_type_name`, `field_name`)
	);
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _remove_back_refs //
CREATE PROCEDURE _remove_back_refs()
BEGIN
	DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN
		SIGNAL SQLSTATE 'HY000'
			SET MESSAGE_TEXT = "Some back references can't be removed";
	END;

	CALL _init_field_shadow(@db_name);
	CALL QEXEC(CONCAT(
		"UPDATE `", @db_name, "`.`field`
		SET `back_ref_name` = NULL
		WHERE `id` IN (
			SELECT `id`
			FROM `nz_admin_db`.`field_shadow`
			JOIN `nz_admin_db`.`backref_upd_list`
			ON `field_owner_type_name` = `owner_type_name`
				AND `field_name` = `name`
		)"
	));

	-- Remove back references pairs
	CALL QEXEC(CONCAT(
		"UPDATE `", @db_name, "`.`field` AS f1
		JOIN `", @db_name, "`.`field` AS f2
		ON f2.`id` = f1.`back_ref_id`
			AND f1.`back_ref_name` IS NULL
		SET f2.`back_ref_name` = NULL"
	));
	CALL QEXEC(CONCAT(
		"UPDATE `", @db_name, "`.`field`
		SET `back_ref_id` = NULL
		WHERE `back_ref_name` IS NULL;"
	));

	DELETE FROM `nz_admin_db`.`backref_upd_list`
	WHERE `new_back_ref_name` IS NULL;
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _add_back_refs //
CREATE PROCEDURE _add_back_refs()
BEGIN
	DECLARE f_id INT UNSIGNED DEFAULT 0;
	DECLARE f_new_back_ref_name VARCHAR(128);

	DECLARE done BOOLEAN DEFAULT FALSE;
	DECLARE back_refs_cur CURSOR FOR
		SELECT `id`, `new_back_ref_name`
		FROM `nz_admin_db`.`field_shadow`
		JOIN `nz_admin_db`.`backref_upd_list`
		ON `field_owner_type_name` = `owner_type_name`
			AND `field_name` = `name`;
	DECLARE CONTINUE HANDLER FOR NOT FOUND
		SET done = TRUE;

	DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN
		SIGNAL SQLSTATE 'HY000'
			SET MESSAGE_TEXT = "Some back references can't be added";
	END;

	CALL _init_field_shadow(@db_name);

	OPEN back_refs_cur;
	FETCH back_refs_cur
	INTO f_id, f_new_back_ref_name;
	WHILE NOT done DO
		CALL QEXEC(CONCAT(
			"UPDATE `", @db_name, "`.`field`
			SET `back_ref_name` = '", f_new_back_ref_name, "'
			WHERE `id` = ", f_id, ";"
		));
		FETCH back_refs_cur
		INTO f_id, f_new_back_ref_name;
	END WHILE;
	CLOSE back_refs_cur;

-- Refresh back references
    CALL QEXEC(CONCAT(
		"UPDATE `", @db_name, "`.`field` AS f1
		JOIN `", @db_name, "`.`field` AS f2
		ON f2.`name` = f1.`back_ref_name`
			AND f2.`ref_type_id` = f1.`owner_type_id`
		SET f1.`back_ref_id` = f2.`id`;"
	));
	CALL QEXEC(CONCAT(
		"UPDATE `", @db_name, "`.`field` AS f1
		JOIN `", @db_name, "`.`field` AS f2
		ON f2.`id` = f1.`back_ref_id`
		SET f2.`back_ref_id` = f1.`id`,
			f2.`back_ref_name` = f1.`name`;"
	));

-- Check if all back references were added
	CALL QEXEC(CONCAT(
		"SELECT COUNT(`id`)
		INTO @back_refs_left
		FROM `", @db_name, "`.`field`
		WHERE NOT `back_ref_name` IS NULL
			AND `back_ref_id` IS NULL;"
	));
	IF @back_refs_left > 0 THEN
		SIGNAL SQLSTATE '45000';
	END IF;
END //



/* ********************************************

		Create Nezaboodka users
			and grant rights for databases

********************************************* */

CREATE USER `nz_admin`@'%' IDENTIFIED BY  'nezaboodka';
GRANT ALL ON *.* TO `nz_admin`@'%';

-- Localhost user
CREATE USER `nz_admin`@'localhost' IDENTIFIED BY  'nezaboodka';
GRANT ALL ON *.* TO `nz_admin`@'localhost';

FLUSH PRIVILEGES;

